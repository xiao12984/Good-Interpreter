namespace GoodInterpreter.Launcher.Models;

/// <summary>
/// 字幕浮窗从后端 /ws/captions 接收的统一消息模型。
/// </summary>
public sealed class CaptionMessage
{
    /// <summary>
    /// 消息类型，caption 表示字幕内容，status 表示服务状态。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 后端翻译会话 ID，用于浮窗区分网页会话和原生浮窗会话。
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 当前原文字幕。
    /// </summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>
    /// 当前译文字幕。
    /// </summary>
    public string TargetText { get; set; } = string.Empty;

    /// <summary>
    /// 原文语言代码，例如 zh 或 en。
    /// </summary>
    public string SourceLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 译文语言代码，例如 en 或 zh。
    /// </summary>
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 是否为当前句子的最终结果。
    /// </summary>
    public bool IsFinal { get; set; }

    /// <summary>
    /// 状态消息值，例如 idle。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 状态或错误的补充说明。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
