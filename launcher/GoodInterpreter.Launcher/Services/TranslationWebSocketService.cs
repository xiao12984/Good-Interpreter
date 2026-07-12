using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GoodInterpreter.Launcher.Config;
using GoodInterpreter.Launcher.Models;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// 浮窗专用翻译 WebSocket 客户端，负责 start/audio/stop 控制消息。
/// </summary>
public sealed class TranslationWebSocketService : IDisposable
{
    /// <summary>
    /// JSON 反序列化选项。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 翻译 WebSocket 地址。
    /// </summary>
    private readonly Uri _webSocketUri = new Uri(LauncherConstants.TranslationWebSocketUrl);

    /// <summary>
    /// 当前 WebSocket。
    /// </summary>
    private ClientWebSocket? _webSocket;

    /// <summary>
    /// 接收循环取消源。
    /// </summary>
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// 发送锁，避免多个音频回调同时写 WebSocket。
    /// </summary>
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// 等待后端两个 AST 会话 ready 的完成源。
    /// </summary>
    private TaskCompletionSource<bool>? _readyCompletionSource;

    /// <summary>
    /// 当前会话 ID。
    /// </summary>
    public string CurrentSessionId { get; private set; } = string.Empty;

    /// <summary>
    /// 当前是否已经连接。
    /// </summary>
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    /// <summary>
    /// 状态变化事件。
    /// </summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// 会话创建事件。
    /// </summary>
    public event Action<string>? SessionCreated;

    /// <summary>
    /// 连接翻译 WebSocket 并发送 start。
    /// </summary>
    public async Task StartAsync(AudioInputMode mode, CancellationToken cancellationToken)
    {
        await StopAsync();

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readyCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _webSocket = new ClientWebSocket();

        StatusChanged?.Invoke("正在连接翻译服务...");
        await _webSocket.ConnectAsync(_webSocketUri, _cancellationTokenSource.Token);

        string audioFormat = mode == AudioInputMode.Microphone ? "wav" : "pcm";
        await SendJsonAsync(new
        {
            type = "start",
            sourceLanguage = "zh",
            targetLanguage = "en",
            audioFormat,
        }, _cancellationTokenSource.Token);

        _ = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token));
        await WaitForReadyAsync(_cancellationTokenSource.Token);
    }

    /// <summary>
    /// 发送一段音频；麦克风模式下调用方会先发送 WAV 头。
    /// </summary>
    public Task SendAudioAsync(byte[] audioBytes)
    {
        if (!IsConnected || audioBytes.Length == 0)
        {
            return Task.CompletedTask;
        }

        return SendJsonAsync(new
        {
            type = "audio",
            data = Convert.ToBase64String(audioBytes),
        }, _cancellationTokenSource?.Token ?? CancellationToken.None);
    }

    /// <summary>
    /// 停止翻译会话并关闭 WebSocket。
    /// </summary>
    public async Task StopAsync()
    {
        ClientWebSocket? webSocket = _webSocket;
        _webSocket = null;

        if (webSocket != null)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    using CancellationTokenSource stopCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    // 停止翻译是 UI 触发路径，给关闭握手加短超时，避免后端或网络异常时浮窗卡死。
                    await SendJsonAsync(new { type = "stop" }, stopCancellation.Token, webSocket);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", stopCancellation.Token);
                }
            }
            catch
            {
                // 停止时连接可能已经断开，忽略即可。
            }
            finally
            {
                webSocket.Dispose();
            }
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _readyCompletionSource = null;
        CurrentSessionId = string.Empty;
        StatusChanged?.Invoke("翻译已停止。");
    }

    /// <summary>
    /// 等待后端确认 AST 双向会话 ready，避免过早发送音频。
    /// </summary>
    private async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        if (_readyCompletionSource == null)
        {
            return;
        }

        await _readyCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
    }

    /// <summary>
    /// 接收后端状态和会话 ID。
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        ClientWebSocket? webSocket = _webSocket;
        if (webSocket == null)
        {
            return;
        }

        byte[] buffer = new byte[8192];

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using MemoryStream messageStream = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _readyCompletionSource?.TrySetException(new InvalidOperationException("翻译服务已断开。"));
                        StatusChanged?.Invoke("翻译服务已断开。");
                        return;
                    }

                    messageStream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                HandleMessage(Encoding.UTF8.GetString(messageStream.ToArray()));
            }
        }
        catch (OperationCanceledException)
        {
            // 停止翻译时取消接收循环是正常路径。
        }
        catch (Exception ex)
        {
            _readyCompletionSource?.TrySetException(new InvalidOperationException("翻译连接异常：" + ex.Message, ex));
            StatusChanged?.Invoke("翻译连接异常：" + ex.Message);
        }
    }

    /// <summary>
    /// 处理后端返回的 JSON 消息。
    /// </summary>
    private void HandleMessage(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("type", out JsonElement typeElement))
        {
            return;
        }

        string type = typeElement.GetString() ?? string.Empty;

        if (type == "sessionCreated" &&
            document.RootElement.TryGetProperty("sessionId", out JsonElement sessionIdElement))
        {
            CurrentSessionId = sessionIdElement.GetString() ?? string.Empty;
            SessionCreated?.Invoke(CurrentSessionId);
            return;
        }

        if (type == "status" &&
            document.RootElement.TryGetProperty("status", out JsonElement statusElement))
        {
            if (statusElement.GetString() == "ready")
            {
                _readyCompletionSource?.TrySetResult(true);
                StatusChanged?.Invoke("翻译服务已就绪。");
            }
            else
            {
                StatusChanged?.Invoke("翻译状态已更新。");
            }

            return;
        }

        if (type == "error" &&
            document.RootElement.TryGetProperty("message", out JsonElement messageElement))
        {
            string message = messageElement.GetString() ?? "翻译服务返回错误。";
            _readyCompletionSource?.TrySetException(new InvalidOperationException(message));
            StatusChanged?.Invoke(message);
        }
    }

    /// <summary>
    /// 发送 JSON 对象到 WebSocket。
    /// </summary>
    private async Task SendJsonAsync(object payload, CancellationToken cancellationToken, ClientWebSocket? explicitWebSocket = null)
    {
        ClientWebSocket? webSocket = explicitWebSocket ?? _webSocket;
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            return;
        }

        string json = JsonSerializer.Serialize(payload, JsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// 释放 WebSocket 资源。
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
        _sendLock.Dispose();
    }
}
