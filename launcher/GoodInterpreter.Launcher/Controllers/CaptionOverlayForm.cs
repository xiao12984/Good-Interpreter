using GoodInterpreter.Launcher.Config;
using GoodInterpreter.Launcher.Models;
using GoodInterpreter.Launcher.Services;
using GoodInterpreter.Launcher.Utils;

namespace GoodInterpreter.Launcher.Controllers;

/// <summary>
/// WinForms 原生字幕浮窗控制台，负责独立显示字幕、采集音频和控制翻译流程。
/// </summary>
public sealed class CaptionOverlayForm : Form
{
    /// <summary>
    /// 无边框窗口拖拽缩放热区宽度。
    /// </summary>
    private const int ResizeGripSize = 8;

    /// <summary>
    /// 字幕浮窗默认宽度。
    /// </summary>
    private const int DefaultOverlayWidth = 980;

    /// <summary>
    /// 字幕浮窗默认高度。
    /// </summary>
    private const int DefaultOverlayHeight = 360;

    private const int WmNcHitTest = 0x0084;
    private const int WmSetRedraw = 0x000B;
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    /// <summary>
    /// 后端服务控制对象，由应用上下文注入，避免浮窗自己重复创建后端进程。
    /// </summary>
    private readonly LauncherService _launcherService;

    /// <summary>
    /// 显示主窗口回调。
    /// </summary>
    private readonly Action _showMainWindow;

    /// <summary>
    /// 退出整个程序回调。
    /// </summary>
    private readonly Action _exitApplication;

    /// <summary>
    /// 只读字幕广播连接。
    /// </summary>
    private readonly CaptionWebSocketService _captionService = new CaptionWebSocketService();

    /// <summary>
    /// 原生音频采集服务。
    /// </summary>
    private readonly NativeAudioCaptureService _audioCaptureService = new NativeAudioCaptureService();

    /// <summary>
    /// 翻译控制 WebSocket 服务。
    /// </summary>
    private readonly TranslationWebSocketService _translationService = new TranslationWebSocketService();

    /// <summary>
    /// 会议导出和总结服务。
    /// </summary>
    private readonly MeetingTranscriptService _meetingService = new MeetingTranscriptService();

    private readonly Label _titleLabel = new Label();
    private readonly Label _statusLabel = new Label();
    private readonly RichTextBox _subtitleBox = new RichTextBox();
    private readonly ContextMenuStrip _contextMenu = new ContextMenuStrip();
    private readonly ToolStripMenuItem _startStopMenuItem = new ToolStripMenuItem("开始翻译");
    private readonly ToolStripMenuItem _microphoneModeMenuItem = new ToolStripMenuItem("麦克风");
    private readonly ToolStripMenuItem _systemAudioModeMenuItem = new ToolStripMenuItem("系统音频");
    private readonly ToolStripMenuItem _deviceMenuItem = new ToolStripMenuItem("设备");

    private readonly List<SubtitleRecord> _subtitleRecords = new List<SubtitleRecord>();
    private readonly List<AudioDeviceInfo> _currentAudioDevices = new List<AudioDeviceInfo>();
    private SubtitleRecord? _currentRecord;
    private string _activeSessionId = string.Empty;
    private string _selectedDeviceId = string.Empty;
    private AudioInputMode _selectedInputMode = AudioInputMode.Microphone;
    private bool _isTranslationRunning;
    private bool _isApplicationExiting;
    /// <summary>
    /// 标记右键菜单是否正在显示，避免实时字幕重绘与菜单消息循环争抢 UI 线程。
    /// </summary>
    private bool _isContextMenuOpen;
    private int _isAudioSendInProgress;
    private float _fontScale = 1.0f;
    private Font? _metaFont;
    private Font? _sourceFont;
    private Font? _targetFont;
    private string _lastRenderedSubtitleSignature = string.Empty;

    /// <summary>
    /// 释放鼠标捕获，让 Windows 接管无边框窗口拖动。
    /// </summary>
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    /// <summary>
    /// 发送窗口消息，用于无边框拖动和 RichTextBox 暂停重绘。
    /// </summary>
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wordParam, IntPtr longParam);

    /// <summary>
    /// 当前浮窗是否正在进行原生翻译。
    /// </summary>
    public bool IsTranslationRunning => _isTranslationRunning;

    /// <summary>
    /// 创建字幕浮窗控制台。
    /// </summary>
    public CaptionOverlayForm(LauncherService launcherService, Action showMainWindow, Action exitApplication)
    {
        _launcherService = launcherService;
        _showMainWindow = showMainWindow;
        _exitApplication = exitApplication;

        Text = "字幕浮窗";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(DefaultOverlayWidth, DefaultOverlayHeight);
        MinimumSize = new Size(720, 260);
        BackColor = Color.FromArgb(18, 24, 38);
        ForeColor = Color.White;
        Opacity = 0.94;
        DoubleBuffered = true;
        KeyPreview = true;
        ResizeRedraw = true;

        BuildLayout();
        BuildContextMenu();
        ApplyFonts();
        RefreshAudioDevices(AudioInputMode.Microphone);
        MoveToDockPosition(CaptionDockPosition.Bottom);
        WireEvents();
        RenderSubtitles();
    }

    /// <summary>
    /// 应用上下文请求真正退出程序。
    /// </summary>
    public void RequestApplicationExit()
    {
        _isApplicationExiting = true;
        StopTranslationForClosing();

        if (!IsDisposed)
        {
            Close();
        }
    }

    /// <summary>
    /// 构建浮窗标题区和字幕区布局，操作入口统一放到右键菜单。
    /// </summary>
    private void BuildLayout()
    {
        TableLayoutPanel rootPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14),
            BackColor = BackColor
        };

        rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Panel titlePanel = CreateTitlePanel();

        _subtitleBox.Dock = DockStyle.Fill;
        _subtitleBox.ReadOnly = true;
        _subtitleBox.BorderStyle = BorderStyle.None;
        _subtitleBox.BackColor = Color.FromArgb(11, 18, 32);
        _subtitleBox.ForeColor = Color.White;
        _subtitleBox.ScrollBars = RichTextBoxScrollBars.None;
        _subtitleBox.DetectUrls = false;
        _subtitleBox.HideSelection = false;
        _subtitleBox.ShortcutsEnabled = false;

        rootPanel.Controls.Add(titlePanel, 0, 0);
        rootPanel.Controls.Add(_subtitleBox, 0, 1);
        Controls.Add(rootPanel);
        AttachResizeHandlers(this);
        AttachDragHandlers(this);
    }

    /// <summary>
    /// 创建可拖动的标题栏。
    /// </summary>
    private Panel CreateTitlePanel()
    {
        Panel panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        _titleLabel.Text = "字幕浮窗";
        _titleLabel.AutoSize = true;
        _titleLabel.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
        _titleLabel.ForeColor = Color.White;
        _titleLabel.Location = new Point(2, 7);

        _statusLabel.Text = "等待翻译...";
        _statusLabel.AutoSize = false;
        _statusLabel.Width = 420;
        _statusLabel.Height = 24;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Font = new Font("Microsoft YaHei UI", 9);
        _statusLabel.ForeColor = Color.FromArgb(148, 163, 184);
        _statusLabel.Location = new Point(96, 5);

        panel.Controls.Add(_titleLabel);
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    /// <summary>
    /// 创建右键菜单。
    /// </summary>
    private void BuildContextMenu()
    {
        _contextMenu.Items.Clear();

        _startStopMenuItem.Click += async (_, _) => await ToggleTranslationAsync();
        _contextMenu.Items.Add(_startStopMenuItem);

        ToolStripMenuItem sourceMenuItem = new ToolStripMenuItem("声源");
        _microphoneModeMenuItem.Click += (_, _) => SelectInputMode(AudioInputMode.Microphone);
        _systemAudioModeMenuItem.Click += (_, _) => SelectInputMode(AudioInputMode.SystemAudio);
        sourceMenuItem.DropDownItems.Add(_microphoneModeMenuItem);
        sourceMenuItem.DropDownItems.Add(_systemAudioModeMenuItem);
        _contextMenu.Items.Add(sourceMenuItem);
        _contextMenu.Items.Add(_deviceMenuItem);
        _contextMenu.Items.Add("刷新设备", null, (_, _) => RefreshAudioDevices(_selectedInputMode));
        _contextMenu.Items.Add(new ToolStripSeparator());

        _contextMenu.Items.Add("导出会议纪要", null, async (_, _) => await ExportTranscriptAsync());
        _contextMenu.Items.Add("总结会议纪要", null, async (_, _) => await SummarizeTranscriptAsync());
        _contextMenu.Items.Add("清空字幕", null, (_, _) => ClearSubtitles());
        _contextMenu.Items.Add(new ToolStripSeparator());

        _contextMenu.Items.Add("显示主窗口", null, (_, _) => _showMainWindow());
        _contextMenu.Items.Add("顶部", null, (_, _) => MoveToDockPosition(CaptionDockPosition.Top));
        _contextMenu.Items.Add("底部", null, (_, _) => MoveToDockPosition(CaptionDockPosition.Bottom));
        _contextMenu.Items.Add("悬浮", null, (_, _) => SetStatusText("已切换为悬浮模式，可直接拖动调整位置。"));
        _contextMenu.Items.Add("置顶开关", null, (_, _) => TopMost = !TopMost);
        _contextMenu.Items.Add("字体放大", null, (_, _) => ScaleFonts(1.1f));
        _contextMenu.Items.Add("字体缩小", null, (_, _) => ScaleFonts(0.9f));
        _contextMenu.Items.Add(new ToolStripSeparator());

        _contextMenu.Items.Add("关闭浮窗", null, (_, _) => Close());
        _contextMenu.Items.Add("退出程序", null, (_, _) => _exitApplication());
        _contextMenu.Opening += (_, _) =>
        {
            // 菜单显示期间暂停字幕框全量重绘，防止翻译消息持续到达时浮窗假死。
            _isContextMenuOpen = true;
            UpdateContextMenuState();
        };
        _contextMenu.Closed += (_, _) =>
        {
            // 菜单关闭后只补绘一次最新字幕，期间到达的中间结果无需逐帧重放。
            _isContextMenuOpen = false;
            RenderSubtitles();
        };
        ContextMenuStrip = _contextMenu;
        ApplyContextMenuToChildren(this);
        UpdateContextMenuState();
    }

    /// <summary>
    /// 将右键菜单绑定到浮窗所有子控件，避免点到字幕框时菜单不出现。
    /// </summary>
    private void ApplyContextMenuToChildren(Control control)
    {
        control.ContextMenuStrip = _contextMenu;

        foreach (Control childControl in control.Controls)
        {
            ApplyContextMenuToChildren(childControl);
        }
    }

    /// <summary>
    /// 绑定所有服务和按钮事件。
    /// </summary>
    private void WireEvents()
    {
        Shown += (_, _) => _captionService.Start();
        FormClosing += (_, _) =>
        {
            if (!_isApplicationExiting)
            {
                StopTranslationForClosing();
            }
        };
        FormClosed += (_, _) => DisposeServices();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };

        _captionService.CaptionReceived += HandleCaptionReceived;
        _captionService.StatusChanged += SetStatusText;
        _audioCaptureService.AudioDataCaptured += HandleAudioDataCaptured;
        _audioCaptureService.StatusChanged += SetStatusText;
        _translationService.StatusChanged += SetStatusText;
        _translationService.SessionCreated += sessionId =>
        {
            _activeSessionId = sessionId;
            SetStatusText("翻译会话已创建。");
        };
    }

    /// <summary>
    /// 根据当前模式刷新右键菜单中的设备列表。
    /// </summary>
    private void RefreshAudioDevices(AudioInputMode mode)
    {
        _currentAudioDevices.Clear();
        _deviceMenuItem.DropDownItems.Clear();

        foreach (AudioDeviceInfo device in _audioCaptureService.GetDevices(mode))
        {
            _currentAudioDevices.Add(device);
        }

        if (_currentAudioDevices.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(_selectedDeviceId) ||
                !_currentAudioDevices.Any(device => device.Id == _selectedDeviceId))
            {
                _selectedDeviceId = _currentAudioDevices[0].Id;
            }

            foreach (AudioDeviceInfo device in _currentAudioDevices)
            {
                ToolStripMenuItem deviceItem = new ToolStripMenuItem(device.Name)
                {
                    Checked = device.Id == _selectedDeviceId,
                    Tag = device
                };

                deviceItem.Click += (_, _) => SelectAudioDevice(device);
                _deviceMenuItem.DropDownItems.Add(deviceItem);
            }

            UpdateContextMenuState();
            return;
        }

        _selectedDeviceId = string.Empty;
        _deviceMenuItem.DropDownItems.Add("未找到可用设备");
        UpdateContextMenuState();
        SetStatusText(mode == AudioInputMode.Microphone ? "未找到麦克风设备。" : "未找到系统音频设备。");
    }

    /// <summary>
    /// 从右键菜单切换声源模式，并同步刷新对应设备列表。
    /// </summary>
    private void SelectInputMode(AudioInputMode mode)
    {
        if (_isTranslationRunning)
        {
            SetStatusText("请先停止翻译，再切换声源。");
            return;
        }

        _selectedInputMode = mode;
        _selectedDeviceId = string.Empty;
        RefreshAudioDevices(mode);
        SetStatusText(mode == AudioInputMode.Microphone ? "已选择麦克风输入。" : "已选择系统音频输入。");
    }

    /// <summary>
    /// 从右键菜单选择具体音频设备。
    /// </summary>
    private void SelectAudioDevice(AudioDeviceInfo device)
    {
        if (_isTranslationRunning)
        {
            SetStatusText("请先停止翻译，再切换设备。");
            return;
        }

        _selectedDeviceId = device.Id;
        UpdateContextMenuState();
        SetStatusText("已选择设备：" + device.Name);
    }

    /// <summary>
    /// 根据当前翻译状态刷新右键菜单勾选和可用性。
    /// </summary>
    private void UpdateContextMenuState()
    {
        _startStopMenuItem.Text = _isTranslationRunning ? "停止翻译" : "开始翻译";
        _microphoneModeMenuItem.Checked = _selectedInputMode == AudioInputMode.Microphone;
        _systemAudioModeMenuItem.Checked = _selectedInputMode == AudioInputMode.SystemAudio;
        _microphoneModeMenuItem.Enabled = !_isTranslationRunning;
        _systemAudioModeMenuItem.Enabled = !_isTranslationRunning;
        _deviceMenuItem.Enabled = !_isTranslationRunning && _currentAudioDevices.Count > 0;

        foreach (ToolStripItem item in _deviceMenuItem.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem &&
                menuItem.Tag is AudioDeviceInfo device)
            {
                menuItem.Checked = device.Id == _selectedDeviceId;
            }
        }
    }

    /// <summary>
    /// 启动或停止翻译。
    /// </summary>
    private async Task ToggleTranslationAsync()
    {
        if (_isTranslationRunning)
        {
            await StopTranslationAsync();
            return;
        }

        await StartTranslationAsync();
    }

    /// <summary>
    /// 启动后端、翻译 WebSocket 和原生音频采集。
    /// </summary>
    private async Task StartTranslationAsync()
    {
        if (!PortUtils.IsListening(LauncherConstants.BackendPort) && !_launcherService.StartBackend())
        {
            SetStatusText("请先保存火山 App Key 和 Access Key。");
            return;
        }

        bool ready = await _launcherService.WaitForBackendReadyAsync(TimeSpan.FromSeconds(12), CancellationToken.None);
        if (!ready)
        {
            SetStatusText("服务启动超时，请稍后重试。");
            return;
        }

        AudioDeviceInfo? selectedDevice = _currentAudioDevices
            .FirstOrDefault(device => device.Id == _selectedDeviceId);

        if (selectedDevice == null)
        {
            SetStatusText("请选择音频设备。");
            return;
        }

        AudioInputMode mode = _selectedInputMode;

        try
        {
            SetButtonsEnabled(false);
            _activeSessionId = string.Empty;
            await _translationService.StartAsync(mode, CancellationToken.None);

            if (mode == AudioInputMode.Microphone)
            {
                await _translationService.SendAudioAsync(PcmAudioConverter.CreateStreamingWavHeader());
            }

            _audioCaptureService.Start(mode, selectedDevice.Id);
            _isTranslationRunning = true;
            UpdateContextMenuState();
            SetStatusText("正在翻译...");
        }
        catch (Exception ex)
        {
            await StopTranslationAsync();
            SetStatusText("启动翻译失败：" + ex.Message);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    /// <summary>
    /// 停止原生采集和翻译 WebSocket。
    /// </summary>
    private async Task StopTranslationAsync()
    {
        _audioCaptureService.Stop();
        await _translationService.StopAsync();
        _isTranslationRunning = false;
        SetButtonsEnabled(true);
        UpdateContextMenuState();
        SetStatusText("翻译已停止。");
    }

    /// <summary>
    /// 窗口关闭路径同步停止翻译，并避免停止异常阻断浮窗关闭。
    /// </summary>
    private void StopTranslationForClosing()
    {
        try
        {
            StopTranslationAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            SetStatusText("关闭浮窗时停止翻译失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 将采集到的音频发给翻译 WebSocket。
    /// </summary>
    private void HandleAudioDataCaptured(byte[] audioBytes)
    {
        if (audioBytes.Length == 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref _isAudioSendInProgress, 1) == 1)
        {
            // 实时音频宁可丢掉落后的包，也不能让 WebSocket 发送队列堆积到界面卡死。
            return;
        }

        _ = SendAudioSafelyAsync(audioBytes);
    }

    /// <summary>
    /// 后台发送音频并吞掉停止过程中的连接竞态，避免未观察异常影响浮窗。
    /// </summary>
    private async Task SendAudioSafelyAsync(byte[] audioBytes)
    {
        try
        {
            await _translationService.SendAudioAsync(audioBytes);
        }
        catch (Exception ex)
        {
            SetStatusText("音频发送异常：" + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _isAudioSendInProgress, 0);
        }
    }

    /// <summary>
    /// 处理后端字幕广播并维护历史列表。
    /// </summary>
    private void HandleCaptionReceived(CaptionMessage message)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<CaptionMessage>(HandleCaptionReceived), message);
            return;
        }

        if (!ShouldAcceptCaption(message))
        {
            return;
        }

        SubtitleRecord record = new SubtitleRecord
        {
            SessionId = message.SessionId,
            SourceText = message.SourceText,
            TargetText = message.TargetText,
            SourceLanguage = message.SourceLanguage,
            TargetLanguage = message.TargetLanguage,
            CreatedAt = DateTime.Now,
        };

        if (message.IsFinal && HasCompleteText(record))
        {
            if (!IsDuplicateLastRecord(record))
            {
                _subtitleRecords.Add(record);
            }

            _currentRecord = null;
        }
        else
        {
            _currentRecord = record;
        }

        RenderSubtitles();
    }

    /// <summary>
    /// 判断字幕是否属于当前浮窗会话；未启动原生会话时允许显示网页字幕。
    /// </summary>
    private bool ShouldAcceptCaption(CaptionMessage message)
    {
        return string.IsNullOrWhiteSpace(_activeSessionId) ||
            string.IsNullOrWhiteSpace(message.SessionId) ||
            string.Equals(message.SessionId, _activeSessionId, StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断记录是否已有原文和译文。
    /// </summary>
    private static bool HasCompleteText(SubtitleRecord record)
    {
        return !string.IsNullOrWhiteSpace(record.SourceText) &&
            !string.IsNullOrWhiteSpace(record.TargetText);
    }

    /// <summary>
    /// 避免后端最终字幕和句子完成事件造成重复记录。
    /// </summary>
    private bool IsDuplicateLastRecord(SubtitleRecord record)
    {
        SubtitleRecord? lastRecord = _subtitleRecords.LastOrDefault();
        return lastRecord != null &&
            lastRecord.SourceText == record.SourceText &&
            lastRecord.TargetText == record.TargetText;
    }

    /// <summary>
    /// 重绘字幕历史和当前实时字幕，并自动滚动到底部。
    /// </summary>
    private void RenderSubtitles()
    {
        if (_isContextMenuOpen)
        {
            // ponytail: 数据继续更新，仅延迟昂贵的 RichTextBox 全量重绘。
            return;
        }

        string subtitleSignature = BuildSubtitleSignature();
        if (subtitleSignature == _lastRenderedSubtitleSignature)
        {
            return;
        }

        _lastRenderedSubtitleSignature = subtitleSignature;
        SetSubtitleRedraw(false);
        _subtitleBox.SuspendLayout();

        try
        {
            _subtitleBox.Clear();

            if (_subtitleRecords.Count == 0 && _currentRecord == null)
            {
                AppendSubtitleLine("等待翻译...", _targetFont ?? _subtitleBox.Font, Color.White);
            }
            else
            {
                foreach (SubtitleRecord record in _subtitleRecords)
                {
                    AppendSubtitleRecord(record, false);
                }

                if (_currentRecord != null)
                {
                    AppendSubtitleRecord(_currentRecord, true);
                }
            }

            _subtitleBox.SelectionStart = _subtitleBox.TextLength;
            _subtitleBox.ScrollToCaret();
        }
        finally
        {
            _subtitleBox.ResumeLayout();
            SetSubtitleRedraw(true);
            _subtitleBox.Invalidate();
        }
    }

    /// <summary>
    /// 构造当前字幕内容签名，避免重复消息触发无意义重绘。
    /// </summary>
    private string BuildSubtitleSignature()
    {
        return string.Join(
            "\u001F",
            _subtitleRecords.Select(record => record.SourceText + "\u001E" + record.TargetText)
                .Append(_currentRecord?.SourceText ?? string.Empty)
                .Append(_currentRecord?.TargetText ?? string.Empty));
    }

    /// <summary>
    /// 暂停或恢复 RichTextBox 重绘，减少实时字幕更新时的闪烁和跳动。
    /// </summary>
    private void SetSubtitleRedraw(bool enabled)
    {
        if (!_subtitleBox.IsHandleCreated)
        {
            return;
        }

        SendMessage(_subtitleBox.Handle, WmSetRedraw, enabled ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// 追加一条双语字幕。
    /// </summary>
    private void AppendSubtitleRecord(SubtitleRecord record, bool isCurrent)
    {
        string prefix = isCurrent ? "实时" : record.CreatedAt.ToString("HH:mm:ss");
        AppendSubtitleText($"[{prefix}] ", _metaFont ?? _subtitleBox.Font, Color.FromArgb(148, 163, 184));
        AppendSubtitleLine(record.SourceText, _sourceFont ?? _subtitleBox.Font, Color.FromArgb(203, 213, 225));
        AppendSubtitleLine(record.TargetText, _targetFont ?? _subtitleBox.Font, Color.White);
        AppendSubtitleLine(string.Empty, _metaFont ?? _subtitleBox.Font, Color.White);
    }

    /// <summary>
    /// 用指定字体和颜色追加文本但不换行，用于把时间戳和原文放在同一行。
    /// </summary>
    private void AppendSubtitleText(string text, Font font, Color color)
    {
        _subtitleBox.SelectionStart = _subtitleBox.TextLength;
        _subtitleBox.SelectionLength = 0;
        _subtitleBox.SelectionFont = font;
        _subtitleBox.SelectionColor = color;
        _subtitleBox.AppendText(text);
    }

    /// <summary>
    /// 用指定字体和颜色向字幕框追加文本。
    /// </summary>
    private void AppendSubtitleLine(string text, Font font, Color color)
    {
        _subtitleBox.SelectionStart = _subtitleBox.TextLength;
        _subtitleBox.SelectionLength = 0;
        _subtitleBox.SelectionFont = font;
        _subtitleBox.SelectionColor = color;
        _subtitleBox.AppendText(text + Environment.NewLine);
    }

    /// <summary>
    /// 导出当前会话的双语会议纪要。
    /// </summary>
    private async Task ExportTranscriptAsync()
    {
        IReadOnlyList<SubtitleRecord> records = await GetExportableRecordsAsync();

        using SaveFileDialog dialog = new SaveFileDialog
        {
            Title = "导出会议纪要",
            Filter = "文本文件 (*.txt)|*.txt",
            FileName = $"会议纪要-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            string text = _meetingService.BuildTranscriptText(records);
            File.WriteAllText(dialog.FileName, text, System.Text.Encoding.UTF8);
            SetStatusText("会议纪要已导出。");
        }
    }

    /// <summary>
    /// 总结当前会话，并显示预览窗口。
    /// </summary>
    private async Task SummarizeTranscriptAsync()
    {
        try
        {
            IReadOnlyList<SubtitleRecord> records = await GetExportableRecordsAsync();
            string summary = await _meetingService.SummarizeAsync(records, CancellationToken.None);
            SummaryPreviewForm previewForm = new SummaryPreviewForm(summary);
            previewForm.Show(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "总结会议纪要", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// 优先从后端数据库读取当前会话；后端未落库时回退到浮窗内存记录。
    /// </summary>
    private async Task<IReadOnlyList<SubtitleRecord>> GetExportableRecordsAsync()
    {
        if (!string.IsNullOrWhiteSpace(_activeSessionId))
        {
            IReadOnlyList<SubtitleRecord> backendRecords = Array.Empty<SubtitleRecord>();

            try
            {
                // 后端落库可能略晚于字幕刷新；读取失败时保留浮窗内存记录作为会议导出来源。
                backendRecords = await _meetingService.GetSessionMessagesAsync(_activeSessionId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                SetStatusText("读取后端会议记录失败，已使用浮窗当前字幕：" + ex.Message);
            }

            if (backendRecords.Count > 0)
            {
                return backendRecords;
            }
        }

        return _subtitleRecords.ToList();
    }

    /// <summary>
    /// 清空浮窗显示的字幕，不删除后端数据库记录。
    /// </summary>
    private void ClearSubtitles()
    {
        _subtitleRecords.Clear();
        _currentRecord = null;
        RenderSubtitles();
    }

    /// <summary>
    /// 设置右键菜单操作是否可用，避免重复启动。
    /// </summary>
    private void SetButtonsEnabled(bool enabled)
    {
        _startStopMenuItem.Enabled = enabled;
        _microphoneModeMenuItem.Enabled = enabled && !_isTranslationRunning;
        _systemAudioModeMenuItem.Enabled = enabled && !_isTranslationRunning;
        _deviceMenuItem.Enabled = enabled && !_isTranslationRunning && _currentAudioDevices.Count > 0;
    }

    /// <summary>
    /// 设置状态栏文本，并自动切回 UI 线程。
    /// </summary>
    private void SetStatusText(string statusText)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetStatusText), statusText);
            return;
        }

        _statusLabel.Text = statusText;
    }

    /// <summary>
    /// 递归绑定鼠标拖动事件，让整个浮窗非缩放热区都可以拖动。
    /// </summary>
    private void AttachDragHandlers(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (GetResizeHitTest(PointToClient(control.PointToScreen(e.Location))) != 0)
            {
                return;
            }

            ScheduleWindowDrag();
        };

        foreach (Control childControl in control.Controls)
        {
            AttachDragHandlers(childControl);
        }
    }

    /// <summary>
    /// 递归绑定缩放热区事件，让子控件覆盖窗口时边缘仍可拖拽缩放。
    /// </summary>
    private void AttachResizeHandlers(Control control)
    {
        control.MouseMove += (_, e) =>
        {
            int hitTest = GetResizeHitTest(PointToClient(control.PointToScreen(e.Location)));
            control.Cursor = GetResizeCursor(hitTest);
        };

        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int hitTest = GetResizeHitTest(PointToClient(control.PointToScreen(e.Location)));
            if (hitTest == 0)
            {
                return;
            }

            ScheduleWindowResize(hitTest);
        };

        control.MouseLeave += (_, _) => control.Cursor = Cursors.Default;

        foreach (Control childControl in control.Controls)
        {
            AttachResizeHandlers(childControl);
        }
    }

    /// <summary>
    /// 延迟启动系统拖动，避免子控件 MouseDown 尚未结束时进入窗口移动循环造成卡死。
    /// </summary>
    private void ScheduleWindowDrag()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(BeginWindowDrag));
    }

    /// <summary>
    /// 延迟启动系统缩放，避免子控件 MouseDown 尚未结束时进入窗口缩放循环造成卡死。
    /// </summary>
    private void ScheduleWindowResize(int hitTest)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() => BeginWindowResize(hitTest)));
    }

    /// <summary>
    /// 使用系统原生标题栏拖动消息移动无边框浮窗，比手写 MouseMove 稳定。
    /// </summary>
    private void BeginWindowDrag()
    {
        ReleaseCapture();
        SendMessage(Handle, WmNcLeftButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
    }

    /// <summary>
    /// 使用系统原生缩放消息调整无边框浮窗尺寸。
    /// </summary>
    private void BeginWindowResize(int hitTest)
    {
        ReleaseCapture();
        SendMessage(Handle, WmNcLeftButtonDown, (IntPtr)hitTest, IntPtr.Zero);
    }

    /// <summary>
    /// 根据鼠标在窗体内的位置判断是否落在缩放热区。
    /// </summary>
    private int GetResizeHitTest(Point cursorPoint)
    {
        bool left = cursorPoint.X <= ResizeGripSize;
        bool right = cursorPoint.X >= Width - ResizeGripSize;
        bool top = cursorPoint.Y <= ResizeGripSize;
        bool bottom = cursorPoint.Y >= Height - ResizeGripSize;

        if (left && top)
        {
            return HtTopLeft;
        }

        if (right && top)
        {
            return HtTopRight;
        }

        if (left && bottom)
        {
            return HtBottomLeft;
        }

        if (right && bottom)
        {
            return HtBottomRight;
        }

        if (left)
        {
            return HtLeft;
        }

        if (right)
        {
            return HtRight;
        }

        if (top)
        {
            return HtTop;
        }

        return bottom ? HtBottom : 0;
    }

    /// <summary>
    /// 将缩放热区转换成对应鼠标样式，便于用户发现可缩放边缘。
    /// </summary>
    private static Cursor GetResizeCursor(int hitTest)
    {
        return hitTest switch
        {
            HtLeft or HtRight => Cursors.SizeWE,
            HtTop or HtBottom => Cursors.SizeNS,
            HtTopLeft or HtBottomRight => Cursors.SizeNWSE,
            HtTopRight or HtBottomLeft => Cursors.SizeNESW,
            _ => Cursors.SizeAll
        };
    }

    /// <summary>
    /// 移动到屏幕顶部或底部。
    /// </summary>
    private void MoveToDockPosition(CaptionDockPosition position)
    {
        Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromControl(this).WorkingArea;
        Left = workingArea.Left + (workingArea.Width - Width) / 2;
        Top = position == CaptionDockPosition.Top
            ? workingArea.Top + 24
            : workingArea.Bottom - Height - 32;
    }

    /// <summary>
    /// 调整字幕字体大小。
    /// </summary>
    private void ScaleFonts(float factor)
    {
        _fontScale = Math.Clamp(_fontScale * factor, 0.72f, 1.45f);
        ApplyFonts();
        RenderSubtitles();
    }

    /// <summary>
    /// 应用字幕字体并释放旧字体资源。
    /// </summary>
    private void ApplyFonts()
    {
        DisposeFonts();
        _metaFont = new Font("Microsoft YaHei UI", 8.5f * _fontScale, FontStyle.Regular);
        _sourceFont = new Font("Microsoft YaHei UI", 12.5f * _fontScale, FontStyle.Regular);
        _targetFont = new Font("Microsoft YaHei UI", 17.5f * _fontScale, FontStyle.Bold);
        _subtitleBox.Font = _sourceFont;
    }

    /// <summary>
    /// 释放字体和服务资源。
    /// </summary>
    private void DisposeServices()
    {
        _captionService.Dispose();
        _audioCaptureService.Dispose();
        _translationService.Dispose();
        _meetingService.Dispose();
        DisposeFonts();
    }

    /// <summary>
    /// 释放浮窗创建的字体对象。
    /// </summary>
    private void DisposeFonts()
    {
        _metaFont?.Dispose();
        _sourceFont?.Dispose();
        _targetFont?.Dispose();
        _metaFont = null;
        _sourceFont = null;
        _targetFont = null;
    }

    /// <summary>
    /// 无边框窗口命中测试，用于四边和四角拖拽缩放。
    /// </summary>
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);

        if (message.Msg != WmNcHitTest)
        {
            return;
        }

        Point cursorPoint = PointToClient(Cursor.Position);
        bool left = cursorPoint.X <= ResizeGripSize;
        bool right = cursorPoint.X >= Width - ResizeGripSize;
        bool top = cursorPoint.Y <= ResizeGripSize;
        bool bottom = cursorPoint.Y >= Height - ResizeGripSize;

        if (left && top)
        {
            message.Result = (IntPtr)HtTopLeft;
            return;
        }
        else if (right && top)
        {
            message.Result = (IntPtr)HtTopRight;
            return;
        }
        else if (left && bottom)
        {
            message.Result = (IntPtr)HtBottomLeft;
            return;
        }
        else if (right && bottom)
        {
            message.Result = (IntPtr)HtBottomRight;
            return;
        }
        else if (left)
        {
            message.Result = (IntPtr)HtLeft;
            return;
        }
        else if (right)
        {
            message.Result = (IntPtr)HtRight;
            return;
        }
        else if (top)
        {
            message.Result = (IntPtr)HtTop;
            return;
        }
        else if (bottom)
        {
            message.Result = (IntPtr)HtBottom;
            return;
        }
    }

    /// <summary>
    /// 浮窗快速停靠位置。
    /// </summary>
    private enum CaptionDockPosition
    {
        /// <summary>
        /// 顶部居中。
        /// </summary>
        Top,

        /// <summary>
        /// 底部居中。
        /// </summary>
        Bottom
    }
}
