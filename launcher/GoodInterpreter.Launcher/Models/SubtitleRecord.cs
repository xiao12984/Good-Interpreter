namespace GoodInterpreter.Launcher.Models;

/// <summary>
/// 字幕浮窗内存中的一条双语字幕记录。
/// </summary>
public sealed class SubtitleRecord
{
    /// <summary>
    /// 所属翻译会话 ID。
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 原文字幕。
    /// </summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>
    /// 译文字幕。
    /// </summary>
    public string TargetText { get; set; } = string.Empty;

    /// <summary>
    /// 原文语言代码。
    /// </summary>
    public string SourceLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 译文语言代码。
    /// </summary>
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 字幕产生时间。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
