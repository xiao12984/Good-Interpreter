namespace GoodInterpreter.Launcher.Models;

/// <summary>
/// 可视化流程节点状态。
/// </summary>
public enum StepState
{
    /// <summary>
    /// 尚未处理。
    /// </summary>
    Pending,

    /// <summary>
    /// 正在处理。
    /// </summary>
    Running,

    /// <summary>
    /// 已完成。
    /// </summary>
    Success,

    /// <summary>
    /// 需要用户处理。
    /// </summary>
    Warning,

    /// <summary>
    /// 处理失败。
    /// </summary>
    Failed
}
