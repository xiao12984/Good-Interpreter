namespace GoodInterpreter.Launcher.Config;

/// <summary>
/// 安装路径集合，负责从启动器所在位置自动定位 Good-Interpreter 安装目录。
/// </summary>
public sealed class AppPaths
{
    /// <summary>
    /// Good-Interpreter 安装根目录。
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// 后端资源目录，安装版用于存放 .env、数据库和 AST 资源。
    /// </summary>
    public string BackendPath { get; }

    /// <summary>
    /// 前端静态资源目录。
    /// </summary>
    public string FrontendPath { get; }

    /// <summary>
    /// 后端环境变量文件路径。
    /// </summary>
    public string EnvFilePath { get; }

    /// <summary>
    /// 安装版内置后端可执行文件路径。
    /// </summary>
    public string BackendExecutablePath { get; }

    /// <summary>
    /// 前端静态构建产物目录，安装版由后端直接托管。
    /// </summary>
    public string FrontendDistPath { get; }

    /// <summary>
    /// 根据安装根目录创建路径集合。
    /// </summary>
    private AppPaths(string rootPath)
    {
        RootPath = rootPath;
        BackendPath = Path.Combine(rootPath, LauncherConstants.BackendFolderName);
        FrontendPath = Path.Combine(rootPath, LauncherConstants.FrontendFolderName);
        EnvFilePath = Path.Combine(BackendPath, ".env");
        BackendExecutablePath = Path.Combine(rootPath, LauncherConstants.BackendExecutableName);
        FrontendDistPath = Path.Combine(FrontendPath, "dist");
    }

    /// <summary>
    /// 从当前运行目录向上查找，直到找到同时包含 backend 和 frontend 的安装目录。
    /// </summary>
    public static AppPaths Discover()
    {
        string? rootPath = FindRepositoryRoot(AppContext.BaseDirectory)
            ?? FindRepositoryRoot(Environment.CurrentDirectory);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new DirectoryNotFoundException("未找到 Good-Interpreter 安装目录，请确认启动器同级或上级目录包含 backend 和 frontend 文件夹。");
        }

        return new AppPaths(rootPath);
    }

    /// <summary>
    /// 从指定目录开始逐级向上查找安装目录。
    /// </summary>
    private static string? FindRepositoryRoot(string startPath)
    {
        DirectoryInfo? current = new DirectoryInfo(startPath);

        while (current != null)
        {
            string backendPath = Path.Combine(current.FullName, LauncherConstants.BackendFolderName);
            string frontendPath = Path.Combine(current.FullName, LauncherConstants.FrontendFolderName);

            if (Directory.Exists(backendPath) && Directory.Exists(frontendPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
