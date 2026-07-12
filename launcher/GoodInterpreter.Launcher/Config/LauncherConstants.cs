namespace GoodInterpreter.Launcher.Config;

/// <summary>
/// 启动器固定配置，统一存放端口、地址和目录名，避免散落在界面代码中。
/// </summary>
public static class LauncherConstants
{
    /// <summary>
    /// 启动器默认窗口宽度，按用户指定的默认分辨率显示。
    /// </summary>
    public const int DefaultWindowWidth = 1562;

    /// <summary>
    /// 启动器默认窗口高度，按用户指定的默认分辨率显示。
    /// </summary>
    public const int DefaultWindowHeight = 976;

    /// <summary>
    /// 后端服务端口，安装版由后端同时提供 API、WebSocket 和前端静态页面。
    /// </summary>
    public const int BackendPort = 3100;

    /// <summary>
    /// 浏览器最终访问的前端地址，安装版访问后端托管的静态页面。
    /// </summary>
    public const string FrontendUrl = "http://localhost:3100";

    /// <summary>
    /// 字幕浮窗连接的只读 WebSocket 地址。
    /// </summary>
    public const string CaptionsWebSocketUrl = "ws://localhost:3100/ws/captions";

    /// <summary>
    /// 浮窗原生翻译控制连接的 WebSocket 地址。
    /// </summary>
    public const string TranslationWebSocketUrl = "ws://localhost:3100/ws";

    /// <summary>
    /// PyInstaller 打包后的内置后端程序名。
    /// </summary>
    public const string BackendExecutableName = "GoodInterpreter.Backend.exe";

    /// <summary>
    /// 后端目录名。
    /// </summary>
    public const string BackendFolderName = "backend";

    /// <summary>
    /// 前端目录名。
    /// </summary>
    public const string FrontendFolderName = "frontend";
}
