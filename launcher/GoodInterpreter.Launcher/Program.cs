using GoodInterpreter.Launcher.Controllers;

namespace GoodInterpreter.Launcher;

/// <summary>
/// 程序入口，仅负责初始化 WinForms 环境并交给应用上下文管理窗口生命周期。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 应用入口方法，不在这里写任何启动业务逻辑。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LauncherApplicationContext());
    }
}
