namespace GoodInterpreter.Launcher.Models;

/// <summary>
/// 音频设备展示模型，用于声源下拉框。
/// </summary>
public sealed class AudioDeviceInfo
{
    /// <summary>
    /// 设备唯一标识。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 设备显示名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 设备所属输入模式。
    /// </summary>
    public AudioInputMode Mode { get; }

    /// <summary>
    /// 创建音频设备信息。
    /// </summary>
    public AudioDeviceInfo(string id, string name, AudioInputMode mode)
    {
        Id = id;
        Name = name;
        Mode = mode;
    }

    /// <summary>
    /// 下拉框默认显示设备名称。
    /// </summary>
    public override string ToString()
    {
        return Name;
    }
}
