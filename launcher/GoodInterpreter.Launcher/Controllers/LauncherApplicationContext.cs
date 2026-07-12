using GoodInterpreter.Launcher.Config;
using GoodInterpreter.Launcher.Services;

namespace GoodInterpreter.Launcher.Controllers;

/// <summary>
/// 应用生命周期控制器，负责让主窗口和字幕浮窗拥有彼此独立的前台/后台行为。
/// </summary>
public sealed class LauncherApplicationContext : ApplicationContext
{
    /// <summary>
    /// 启动器共享服务，主窗口和浮窗共用同一套后端服务控制逻辑。
    /// </summary>
    private readonly LauncherService _launcherService;

    /// <summary>
    /// 主启动器窗口。
    /// </summary>
    private readonly MainForm _mainForm;

    /// <summary>
    /// 独立字幕浮窗，存在时不作为主窗口的子窗口。
    /// </summary>
    private CaptionOverlayForm? _captionOverlayForm;

    /// <summary>
    /// 是否正在执行真正退出，防止普通关闭主窗口时误杀浮窗。
    /// </summary>
    private bool _isExiting;

    /// <summary>
    /// 创建应用上下文并打开主窗口。
    /// </summary>
    public LauncherApplicationContext()
    {
        _launcherService = new LauncherService(AppPaths.Discover());
        _mainForm = new MainForm(_launcherService);
        _mainForm.CaptionOverlayRequested += ShowCaptionOverlay;
        _mainForm.FormClosing += HandleMainFormClosing;
        _mainForm.Show();
    }

    /// <summary>
    /// 打开或激活字幕浮窗；浮窗不设置 Owner，避免被主窗口最小化或关闭牵连。
    /// </summary>
    private void ShowCaptionOverlay()
    {
        if (_captionOverlayForm == null || _captionOverlayForm.IsDisposed)
        {
            _captionOverlayForm = new CaptionOverlayForm(
                _launcherService,
                ShowMainWindow,
                ExitApplication);
            _captionOverlayForm.FormClosed += HandleCaptionOverlayClosed;
            _captionOverlayForm.Show();
            return;
        }

        _captionOverlayForm.Show();
        _captionOverlayForm.WindowState = FormWindowState.Normal;
        _captionOverlayForm.Activate();
    }

    /// <summary>
    /// 当浮窗还在工作时，主窗口关闭只隐藏主窗口，不退出整个程序。
    /// </summary>
    private void HandleMainFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_isExiting)
        {
            return;
        }

        if (_captionOverlayForm is { IsDisposed: false } &&
            (_captionOverlayForm.Visible || _captionOverlayForm.IsTranslationRunning))
        {
            eventArgs.Cancel = true;
            _mainForm.Hide();
            return;
        }

        _isExiting = true;
        _launcherService.StopServices();
        ExitThread();
    }

    /// <summary>
    /// 浮窗关闭后如果主窗口被隐藏，则恢复主窗口，避免应用静默留在后台。
    /// </summary>
    private void HandleCaptionOverlayClosed(object? sender, FormClosedEventArgs eventArgs)
    {
        _captionOverlayForm = null;

        if (!_isExiting && !_mainForm.Visible)
        {
            ShowMainWindow();
        }
    }

    /// <summary>
    /// 显示主窗口并切到前台。
    /// </summary>
    private void ShowMainWindow()
    {
        if (_mainForm.IsDisposed)
        {
            return;
        }

        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
    }

    /// <summary>
    /// 统一退出程序，释放浮窗、音频采集和后端服务。
    /// </summary>
    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _captionOverlayForm?.RequestApplicationExit();
        _launcherService.StopServices();

        if (!_mainForm.IsDisposed)
        {
            _mainForm.FormClosing -= HandleMainFormClosing;
            _mainForm.Close();
        }

        ExitThread();
    }
}
