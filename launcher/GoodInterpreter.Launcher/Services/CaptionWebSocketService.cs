using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GoodInterpreter.Launcher.Config;
using GoodInterpreter.Launcher.Models;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// 字幕浮窗 WebSocket 客户端，负责连接后端只读字幕通道并自动重连。
/// </summary>
public sealed class CaptionWebSocketService : IDisposable
{
    /// <summary>
    /// JSON 反序列化选项，后端使用 camelCase，C# 模型使用 PascalCase。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 字幕通道地址，固定连接本机后端 3100 端口。
    /// </summary>
    private readonly Uri _captionUri = new Uri(LauncherConstants.CaptionsWebSocketUrl);

    /// <summary>
    /// 控制重连循环退出的取消源。
    /// </summary>
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// 后台接收任务，避免重复启动多个连接循环。
    /// </summary>
    private Task? _workerTask;

    /// <summary>
    /// 收到字幕内容时触发。
    /// </summary>
    public event Action<CaptionMessage>? CaptionReceived;

    /// <summary>
    /// 连接状态变化时触发，用于浮窗显示等待或断开提示。
    /// </summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// 启动字幕连接循环；如果已经运行则直接复用。
    /// </summary>
    public void Start()
    {
        if (_workerTask is { IsCompleted: false })
        {
            return;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        _workerTask = Task.Run(() => RunReconnectLoopAsync(_cancellationTokenSource.Token));
    }

    /// <summary>
    /// 请求停止字幕连接循环。
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// 循环连接后端字幕通道，服务未启动或断开时自动重试。
    /// </summary>
    private async Task RunReconnectLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using ClientWebSocket webSocket = new ClientWebSocket();

            try
            {
                StatusChanged?.Invoke("正在连接字幕服务...");
                await webSocket.ConnectAsync(_captionUri, cancellationToken);
                StatusChanged?.Invoke("等待翻译...");
                await ReceiveLoopAsync(webSocket, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                StatusChanged?.Invoke("等待服务启动...");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("字幕连接异常：" + ex.Message);
            }

            await DelayBeforeReconnectAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 接收后端消息；支持分片消息，避免较长字幕被截断。
    /// </summary>
    private async Task ReceiveLoopAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using MemoryStream messageStream = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    StatusChanged?.Invoke("字幕服务已断开，正在重连...");
                    return;
                }

                messageStream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            HandleMessageBytes(messageStream.ToArray());
        }
    }

    /// <summary>
    /// 解析后端 JSON 消息，并分发给浮窗界面。
    /// </summary>
    private void HandleMessageBytes(byte[] messageBytes)
    {
        string json = Encoding.UTF8.GetString(messageBytes);
        CaptionMessage? message = JsonSerializer.Deserialize<CaptionMessage>(json, JsonOptions);

        if (message == null)
        {
            return;
        }

        if (string.Equals(message.Type, "caption", StringComparison.OrdinalIgnoreCase))
        {
            CaptionReceived?.Invoke(message);
            return;
        }

        if (string.Equals(message.Type, "status", StringComparison.OrdinalIgnoreCase))
        {
            StatusChanged?.Invoke(GetStatusText(message));
        }
    }

    /// <summary>
    /// 将后端状态转换为面向用户的浮窗提示。
    /// </summary>
    private static string GetStatusText(CaptionMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Message))
        {
            return message.Message;
        }

        return message.Status switch
        {
            "idle" => "等待翻译...",
            "ready" => "等待翻译...",
            "disconnected" => "字幕服务已断开，正在重连...",
            _ => "等待翻译..."
        };
    }

    /// <summary>
    /// 重连前短暂等待；取消时直接退出，不阻塞窗口关闭。
    /// </summary>
    private static async Task DelayBeforeReconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(1500, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 窗口关闭时取消等待是正常路径，不需要再抛给界面。
        }
    }

    /// <summary>
    /// 释放字幕连接资源。
    /// </summary>
    public void Dispose()
    {
        Stop();
        _cancellationTokenSource?.Dispose();
    }
}
