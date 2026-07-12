using System.Text;
using System.Text.Json;
using GoodInterpreter.Launcher.Config;
using GoodInterpreter.Launcher.Models;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// 会议记录读取、导出文本生成和 AI 总结服务。
/// </summary>
public sealed class MeetingTranscriptService : IDisposable
{
    /// <summary>
    /// JSON 反序列化选项。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 后端 HTTP 客户端。
    /// </summary>
    private readonly HttpClient _httpClient = new HttpClient
    {
        BaseAddress = new Uri(LauncherConstants.FrontendUrl)
    };

    /// <summary>
    /// 获取指定会话的双语记录。
    /// </summary>
    public async Task<IReadOnlyList<SubtitleRecord>> GetSessionMessagesAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Array.Empty<SubtitleRecord>();
        }

        using HttpResponseMessage response = await _httpClient.GetAsync($"/api/sessions/{sessionId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        SessionMessagesResponse? result = JsonSerializer.Deserialize<SessionMessagesResponse>(json, JsonOptions);

        return result?.Messages?
            .Select(message => new SubtitleRecord
            {
                SessionId = message.SessionId,
                SourceText = message.SourceText,
                TargetText = message.TargetText,
                SourceLanguage = message.SourceLanguage,
                TargetLanguage = message.TargetLanguage,
                CreatedAt = ParseDateTime(message.CreatedAt),
            })
            .ToList() ?? new List<SubtitleRecord>();
    }

    /// <summary>
    /// 生成双语会议纪要 TXT 内容，不调用 AI。
    /// </summary>
    public string BuildTranscriptText(IReadOnlyList<SubtitleRecord> records)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Good-Interpreter 会议纪要");
        builder.AppendLine("导出时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine();

        if (records.Count == 0)
        {
            builder.AppendLine("暂无会议内容。");
            return builder.ToString();
        }

        for (int index = 0; index < records.Count; index++)
        {
            SubtitleRecord record = records[index];
            builder.AppendLine($"[{index + 1}] {record.CreatedAt:HH:mm:ss}");
            builder.AppendLine("原文：" + record.SourceText);
            builder.AppendLine("译文：" + record.TargetText);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// 调用后端 AI 总结接口。
    /// </summary>
    public async Task<string> SummarizeAsync(IReadOnlyList<SubtitleRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            throw new InvalidOperationException("暂无会议内容可总结。");
        }

        object payload = new
        {
            messages = records.Select(record => new
            {
                sourceText = record.SourceText,
                targetText = record.TargetText,
            }).ToList()
        };

        string json = JsonSerializer.Serialize(payload, JsonOptions);
        using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync("/api/summarize", content, cancellationToken);
        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        SummaryResponse? result = JsonSerializer.Deserialize<SummaryResponse>(responseJson, JsonOptions);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(result?.Error ?? "总结会议纪要失败。");
        }

        if (string.IsNullOrWhiteSpace(result?.Summary))
        {
            throw new InvalidOperationException("总结会议纪要失败，后端没有返回内容。");
        }

        return result.Summary;
    }

    /// <summary>
    /// 尝试解析后端 ISO 时间。
    /// </summary>
    private static DateTime ParseDateTime(string value)
    {
        return DateTime.TryParse(value, out DateTime parsedValue)
            ? parsedValue
            : DateTime.Now;
    }

    /// <summary>
    /// /api/sessions/{id} 返回模型。
    /// </summary>
    private sealed class SessionMessagesResponse
    {
        /// <summary>
        /// 会话消息列表。
        /// </summary>
        public List<MessageResponse> Messages { get; set; } = new List<MessageResponse>();
    }

    /// <summary>
    /// 后端消息 DTO。
    /// </summary>
    private sealed class MessageResponse
    {
        /// <summary>
        /// 会话 ID。
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 原文。
        /// </summary>
        public string SourceText { get; set; } = string.Empty;

        /// <summary>
        /// 译文。
        /// </summary>
        public string TargetText { get; set; } = string.Empty;

        /// <summary>
        /// 原文语言。
        /// </summary>
        public string SourceLanguage { get; set; } = string.Empty;

        /// <summary>
        /// 译文语言。
        /// </summary>
        public string TargetLanguage { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间。
        /// </summary>
        public string CreatedAt { get; set; } = string.Empty;
    }

    /// <summary>
    /// 总结接口返回模型。
    /// </summary>
    private sealed class SummaryResponse
    {
        /// <summary>
        /// AI 总结内容。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// 释放后端 HTTP 客户端。
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
