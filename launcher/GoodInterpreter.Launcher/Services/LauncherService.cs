using System.Diagnostics;
using GoodInterpreter.Launcher.Config;
using GoodInterpreter.Launcher.Utils;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// 启动器业务服务，负责保存配置、启动内置服务、打开浏览器和停止服务。
/// </summary>
public sealed class LauncherService
{
    private readonly AppPaths _paths;
    private readonly ProcessRunner _processRunner;
    private readonly EnvFileService _envFileService;

    private Process? _backendProcess;

    /// <summary>
    /// 启动器日志事件。
    /// </summary>
    public event Action<string>? LogReceived;

    /// <summary>
    /// 创建启动器服务。
    /// </summary>
    public LauncherService(AppPaths paths)
    {
        _paths = paths;
        _processRunner = new ProcessRunner();
        _envFileService = new EnvFileService(paths);
        _processRunner.OutputReceived += line => LogReceived?.Invoke(line);
    }

    /// <summary>
    /// 当前安装路径。
    /// </summary>
    public AppPaths Paths => _paths;

    /// <summary>
    /// .env 配置服务。
    /// </summary>
    public EnvFileService EnvFileService => _envFileService;

    /// <summary>
    /// 保存火山引擎配置。
    /// </summary>
    public void SaveConfig(string appId, string accessKey)
    {
        _envFileService.SaveVolcengineSettings(appId, accessKey);
        LogReceived?.Invoke("配置已保存，服务端口已固定为 3100。");
    }

    /// <summary>
    /// 启动安装包内置服务，后端 exe 会同时托管 API、WebSocket 和前端静态页面。
    /// </summary>
    public bool StartBackend()
    {
        if (_backendProcess is { HasExited: false })
        {
            LogReceived?.Invoke("服务已经由启动器启动。");
            return true;
        }

        if (PortUtils.IsListening(LauncherConstants.BackendPort))
        {
            LogReceived?.Invoke("服务端口 3100 已被其他进程占用，请先停止旧服务后再启动。");
            return false;
        }

        if (!_envFileService.HasValidVolcengineKeys())
        {
            LogReceived?.Invoke("请先填写并保存火山引擎 App Key 和 Access Key。");
            return false;
        }

        if (!File.Exists(_paths.BackendExecutablePath))
        {
            LogReceived?.Invoke("未找到内置服务程序 GoodInterpreter.Backend.exe，请重新安装或重新打包。");
            return false;
        }

        // 安装版不依赖目标电脑的 Python/Node，所有运行时依赖已经被打进后端 exe。
        _backendProcess = _processRunner.StartServiceProcess(_paths.BackendExecutablePath, string.Empty, _paths.RootPath, "服务");
        return true;
    }

    /// <summary>
    /// 等待内置后端端口就绪，保存配置后一键打开页面时避免浏览器过早访问。
    /// </summary>
    public async Task<bool> WaitForBackendReadyAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.Now.Add(timeout);

        while (DateTime.Now < deadline)
        {
            if (PortUtils.IsListening(LauncherConstants.BackendPort))
            {
                return true;
            }

            await Task.Delay(300, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// 使用默认浏览器打开前端页面。
    /// </summary>
    public void OpenFrontendInBrowser()
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = LauncherConstants.FrontendUrl,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        LogReceived?.Invoke("已请求浏览器打开前端页面。");
    }

    /// <summary>
    /// 停止由启动器创建的服务进程。
    /// </summary>
    public void StopServices()
    {
        _processRunner.StopProcess(_backendProcess, "服务");
    }
}
