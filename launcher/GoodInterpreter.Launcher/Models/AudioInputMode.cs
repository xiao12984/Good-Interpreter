namespace GoodInterpreter.Launcher.Models;

/// <summary>
/// 字幕浮窗支持的音频输入模式。
/// </summary>
public enum AudioInputMode
{
    /// <summary>
    /// 麦克风输入。
    /// </summary>
    Microphone,

    /// <summary>
    /// 系统音频输入，使用 WASAPI loopback 捕获扬声器输出。
    /// </summary>
    SystemAudio
}
