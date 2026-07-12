namespace GoodInterpreter.Launcher.Models;

/// <summary>
/// 安装版运行检查结果项，用于后续诊断入口展示检查状态。
/// </summary>
public sealed class CheckItem
{
    /// <summary>
    /// 检查项名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 检查项是否通过。
    /// </summary>
    public bool IsOk { get; }

    /// <summary>
    /// 面向用户的说明文本。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 创建一条检查结果。
    /// </summary>
    public CheckItem(string name, bool isOk, string message)
    {
        Name = name;
        IsOk = isOk;
        Message = message;
    }
}
