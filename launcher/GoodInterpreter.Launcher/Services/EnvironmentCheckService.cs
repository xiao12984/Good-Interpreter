using GoodInterpreter.Launcher.Config;
using GoodInterpreter.Launcher.Models;
using GoodInterpreter.Launcher.Utils;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// 安装版运行检查服务，集中判断内置服务、静态页面、密钥和端口状态。
/// </summary>
public sealed class EnvironmentCheckService
{
    private readonly AppPaths _paths;
    private readonly EnvFileService _envFileService;

    /// <summary>
    /// 创建安装版运行检查服务。
    /// </summary>
    public EnvironmentCheckService(AppPaths paths, EnvFileService envFileService)
    {
        _paths = paths;
        _envFileService = envFileService;
    }

    /// <summary>
    /// 执行安装版运行检查，结果可供隐藏诊断入口或后续维护界面复用。
    /// </summary>
    public Task<IReadOnlyList<CheckItem>> CheckAsync(CancellationToken cancellationToken)
    {
        // 当前检查只做本地文件和端口判断，保留取消参数方便后续接入耗时诊断。
        _ = cancellationToken;

        List<CheckItem> items = new List<CheckItem>();

        items.Add(new CheckItem("安装目录", Directory.Exists(_paths.RootPath), _paths.RootPath));
        items.Add(new CheckItem("内置服务程序", File.Exists(_paths.BackendExecutablePath), File.Exists(_paths.BackendExecutablePath) ? "已找到 GoodInterpreter.Backend.exe" : "缺少 GoodInterpreter.Backend.exe"));
        items.Add(new CheckItem("AST 资源目录", Directory.Exists(Path.Combine(_paths.BackendPath, "ast_python")), Directory.Exists(Path.Combine(_paths.BackendPath, "ast_python")) ? "已找到 ast_python" : "缺少 backend\\ast_python"));
        items.Add(new CheckItem("前端静态页面", Directory.Exists(_paths.FrontendDistPath), Directory.Exists(_paths.FrontendDistPath) ? "已找到 frontend\\dist" : "缺少 frontend\\dist"));
        items.Add(new CheckItem(".env 配置", File.Exists(_paths.EnvFilePath), File.Exists(_paths.EnvFilePath) ? "已找到 .env" : "请先保存火山引擎配置"));
        items.Add(new CheckItem("火山密钥", _envFileService.HasValidVolcengineKeys(), _envFileService.HasValidVolcengineKeys() ? "已填写" : "仍是空值或占位符"));
        items.Add(new CheckItem("服务端口", !PortUtils.IsListening(LauncherConstants.BackendPort), PortUtils.IsListening(LauncherConstants.BackendPort) ? "3100 已有服务在运行" : "3100 可用"));

        return Task.FromResult<IReadOnlyList<CheckItem>>(items);
    }
}
