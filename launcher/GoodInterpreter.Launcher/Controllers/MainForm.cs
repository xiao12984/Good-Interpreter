using GoodInterpreter.Launcher.Config;
using GoodInterpreter.Launcher.Models;
using GoodInterpreter.Launcher.Services;
using GoodInterpreter.Launcher.Utils;
using ReaLTaiizor.Forms;
using MaterialButton = ReaLTaiizor.Controls.MaterialButton;

namespace GoodInterpreter.Launcher.Controllers;

/// <summary>
/// Good-Interpreter 可视化启动主窗口，负责展示流程和响应用户按钮操作。
/// </summary>
public sealed class MainForm : MaterialForm
{
    private readonly LauncherService _launcherService;
    private readonly Dictionary<string, Label> _stepLabels = new Dictionary<string, Label>();

    private TextBox _appIdTextBox = new TextBox();
    private TextBox _accessKeyTextBox = new TextBox();
    private RichTextBox _logBox = new RichTextBox();
    private Label _serviceStatusLabel = new Label();

    /// <summary>
    /// 请求应用上下文打开字幕浮窗，主窗口不直接持有浮窗生命周期。
    /// </summary>
    public event Action? CaptionOverlayRequested;

    /// <summary>
    /// 创建主窗口并初始化可视化流程。
    /// </summary>
    public MainForm(LauncherService launcherService)
    {
        _launcherService = launcherService;
        _launcherService.LogReceived += AppendLog;

        Text = "Good-Interpreter 可视化启动器";
        ApplyWindowIcon();
        Size = new Size(LauncherConstants.DefaultWindowWidth, LauncherConstants.DefaultWindowHeight);
        MinimumSize = new Size(1040, 680);
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(14, 74, 14, 14);
        BackColor = Color.FromArgb(245, 247, 250);

        BuildLayout();
        LoadEnvValues();
        RefreshServiceBadge();
        RefreshDetectedWorkflowStates(writeDependencyLogs: true);
    }

    /// <summary>
    /// 从当前 EXE 提取应用图标并设置到窗口，保证任务栏和窗口标题栏图标一致。
    /// </summary>
    private void ApplyWindowIcon()
    {
        using Icon? appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        if (appIcon != null)
        {
            // 克隆图标后再赋给窗口，避免临时 Icon 被释放后影响标题栏显示。
            Icon = (Icon)appIcon.Clone();
        }
    }

    /// <summary>
    /// 构建窗口布局，所有控件都在代码中创建，避免依赖设计器文件。
    /// </summary>
    private void BuildLayout()
    {
        TableLayoutPanel rootPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = BackColor
        };

        rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        Panel headerPanel = CreateHeaderPanel();
        rootPanel.Controls.Add(headerPanel, 0, 0);
        rootPanel.SetColumnSpan(headerPanel, 2);

        Panel leftPanel = CreateLeftPanel();
        rootPanel.Controls.Add(leftPanel, 0, 1);

        Panel rightPanel = CreateRightPanel();
        rootPanel.Controls.Add(rightPanel, 1, 1);

        Panel footerPanel = CreateFooterPanel();
        rootPanel.Controls.Add(footerPanel, 0, 2);
        rootPanel.SetColumnSpan(footerPanel, 2);

        Controls.Add(rootPanel);
    }

    /// <summary>
    /// 创建顶部说明区，展示项目路径和端口状态。
    /// </summary>
    private Panel CreateHeaderPanel()
    {
        Panel panel = CreateCardPanel();
        panel.Padding = new Padding(18);

        Label titleLabel = new Label
        {
            Text = "同声传译一键启动",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(18, 18)
        };

        Label pathLabel = new Label
        {
            Text = "安装目录：" + _launcherService.Paths.RootPath,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 64)
        };

        _serviceStatusLabel = CreateStatusBadge("服务 3100：检查中", 20, 94);

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(pathLabel);
        panel.Controls.Add(_serviceStatusLabel);

        return panel;
    }

    /// <summary>
    /// 创建左侧配置和操作按钮区域。
    /// </summary>
    private Panel CreateLeftPanel()
    {
        Panel panel = CreateCardPanel();
        panel.Padding = new Padding(18);
        panel.AutoScroll = true;

        Label configTitle = CreateSectionTitle("配置");
        configTitle.Location = new Point(18, 18);

        Label appIdLabel = CreateInputLabel("火山 App Key", 18, 58);
        _appIdTextBox = CreateInputBox(18, 84, false);

        Label accessKeyLabel = CreateInputLabel("火山 Access Key", 18, 132);
        _accessKeyTextBox = CreateInputBox(18, 158, true);

        // 普通用户分步操作，避免保存配置和启动服务混在一个按钮里。
        MaterialButton saveButton = CreateActionButton("保存key", 18, 204, Color.FromArgb(16, 185, 129));
        saveButton.Click += (_, _) => { SaveConfig(); };

        MaterialButton startButton = CreateActionButton("启动服务", 18, 258, Color.FromArgb(14, 165, 233));
        startButton.Click += async (_, _) => await StartServicesAsync();

        MaterialButton openButton = CreateActionButton("打开网页前台", 18, 312, Color.FromArgb(99, 102, 241));
        openButton.Click += (_, _) => OpenFrontend();

        MaterialButton captionButton = CreateActionButton("字幕浮窗", 18, 366, Color.FromArgb(71, 85, 105));
        captionButton.Click += (_, _) => OpenCaptionOverlay();

        MaterialButton stopButton = CreateActionButton("停止服务", 18, 420, Color.FromArgb(220, 38, 38));
        stopButton.Click += (_, _) => StopServices();

        panel.Controls.AddRange(new Control[]
        {
            configTitle,
            appIdLabel,
            _appIdTextBox,
            accessKeyLabel,
            _accessKeyTextBox,
            saveButton,
            startButton,
            openButton,
            captionButton,
            stopButton
        });

        return panel;
    }

    /// <summary>
    /// 创建右侧流程时间线和日志区域。
    /// </summary>
    private Panel CreateRightPanel()
    {
        Panel panel = CreateCardPanel();
        panel.Padding = new Padding(18);

        // 右侧区域使用表格布局，让日志框在默认窗口和拉伸窗口下稳定填满剩余空间。
        TableLayoutPanel contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };

        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 154));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Label flowTitle = CreateSectionTitle("流程状态");
        flowTitle.Dock = DockStyle.Fill;
        flowTitle.TextAlign = ContentAlignment.MiddleLeft;

        FlowLayoutPanel flowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent
        };

        AddStepCard(flowPanel, "保存配置");
        AddStepCard(flowPanel, "启动服务");
        AddStepCard(flowPanel, "网页前台");

        Label logTitle = CreateSectionTitle("实时日志");
        logTitle.Dock = DockStyle.Fill;
        logTitle.TextAlign = ContentAlignment.MiddleLeft;

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.FromArgb(226, 232, 240),
            Font = new Font("Microsoft YaHei UI", 9.5f),
            ReadOnly = true
        };

        contentLayout.Controls.Add(flowTitle, 0, 0);
        contentLayout.Controls.Add(flowPanel, 0, 1);
        contentLayout.Controls.Add(logTitle, 0, 2);
        contentLayout.Controls.Add(_logBox, 0, 3);
        panel.Controls.Add(contentLayout);

        return panel;
    }

    /// <summary>
    /// 创建底部提示区，帮助用户理解首次使用只需要填写火山密钥。
    /// </summary>
    private Panel CreateFooterPanel()
    {
        Panel panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        Label noteLabel = new Label
        {
            Text = "提示：首次使用先保存key；字幕浮窗可单独打开，服务和网页前台按需启动。",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(18, 20)
        };

        panel.Controls.Add(noteLabel);
        return panel;
    }

    /// <summary>
    /// 从 .env 读取已有配置并填入输入框。
    /// </summary>
    private void LoadEnvValues()
    {
        Dictionary<string, string> values = _launcherService.EnvFileService.LoadValues();

        if (values.TryGetValue("VOLC_APP_ID", out string? appId))
        {
            _appIdTextBox.Text = appId;
        }

        if (values.TryGetValue("VOLC_ACCESS_KEY", out string? accessKey))
        {
            _accessKeyTextBox.Text = accessKey;
        }
    }

    /// <summary>
    /// 保存配置按钮逻辑。
    /// </summary>
    private bool SaveConfig()
    {
        if (string.IsNullOrWhiteSpace(_appIdTextBox.Text) || string.IsNullOrWhiteSpace(_accessKeyTextBox.Text))
        {
            // 密钥为空时不写入 .env，避免生成看似有效但无法连接火山 AST 的配置。
            AppendLog("请先填写 App Key 和 Access Key。");
            SetStepState("保存配置", StepState.Warning);
            return false;
        }

        _launcherService.SaveConfig(_appIdTextBox.Text, _accessKeyTextBox.Text);
        SetStepState("保存配置", StepState.Success);
        RefreshServiceBadge();
        return true;
    }

    /// <summary>
    /// 启动服务按钮逻辑，只负责启动后端并等待端口就绪。
    /// </summary>
    private async Task StartServicesAsync()
    {
        if (!StartAllServices())
        {
            return;
        }

        bool ready = await _launcherService.WaitForBackendReadyAsync(TimeSpan.FromSeconds(12), CancellationToken.None);
        RefreshServiceBadge();

        if (!ready)
        {
            AppendLog("服务启动超时，请稍后再点击“启动服务”或查看实时日志。");
            return;
        }

        AppendLog("服务已就绪，可以点击“打开网页前台”。");
    }

    /// <summary>
    /// 启动内置服务按钮逻辑：安装版只需要启动一个后端服务，由它托管前端页面。
    /// </summary>
    private bool StartAllServices()
    {
        if (PortUtils.IsListening(LauncherConstants.BackendPort))
        {
            AppendLog("检测到服务 3100 已启动。");
            SetStepState("启动服务", StepState.Success);
            RefreshServiceBadge();
            return true;
        }

        SetStepState("启动服务", StepState.Running);
        bool backendSuccess = _launcherService.StartBackend();
        SetStepState("启动服务", backendSuccess ? StepState.Success : StepState.Failed);

        RefreshServiceBadge();
        return backendSuccess;
    }

    /// <summary>
    /// 打开网页前台按钮逻辑。
    /// </summary>
    private void OpenFrontend()
    {
        if (!PortUtils.IsListening(LauncherConstants.BackendPort))
        {
            AppendLog("请先点击“启动服务”，服务就绪后再打开网页前台。");
            SetStepState("网页前台", StepState.Warning);
            RefreshServiceBadge();
            return;
        }

        _launcherService.OpenFrontendInBrowser();
        SetStepState("网页前台", StepState.Success);
    }

    /// <summary>
    /// 打开字幕浮窗；只确保服务启动，不自动打开浏览器页面。
    /// </summary>
    private void OpenCaptionOverlay()
    {
        if (!PortUtils.IsListening(LauncherConstants.BackendPort) && !StartAllServices())
        {
            return;
        }

        CaptionOverlayRequested?.Invoke();
        AppendLog("已打开字幕浮窗，浮窗可独立开始或停止翻译。");
    }

    /// <summary>
    /// 停止服务按钮逻辑。
    /// </summary>
    private void StopServices()
    {
        _launcherService.StopServices();
        SetStepState("启动服务", StepState.Pending);
        RefreshServiceBadge();
    }

    /// <summary>
    /// 刷新服务端口状态标签。
    /// </summary>
    private void RefreshServiceBadge()
    {
        bool serviceListening = PortUtils.IsListening(LauncherConstants.BackendPort);

        _serviceStatusLabel.Text = serviceListening ? "服务 3100：已启动" : "服务 3100：未启动";

        _serviceStatusLabel.BackColor = serviceListening ? Color.FromArgb(220, 252, 231) : Color.FromArgb(254, 243, 199);
    }

    /// <summary>
    /// 启动窗口时同步本地状态，避免端口和流程卡显示不一致。
    /// </summary>
    private void RefreshDetectedWorkflowStates(bool writeDependencyLogs)
    {
        bool configReady = _launcherService.EnvFileService.HasValidVolcengineKeys();
        bool serviceReady = PortUtils.IsListening(LauncherConstants.BackendPort);

        SetStepState("保存配置", configReady ? StepState.Success : StepState.Pending);
        SetStepState("启动服务", serviceReady ? StepState.Success : StepState.Pending);

        if (!writeDependencyLogs)
        {
            return;
        }

        if (configReady)
        {
            AppendLog("检测到火山配置已保存，无需重复保存配置。");
        }

        if (serviceReady)
        {
            AppendLog("检测到服务 3100 已启动，可以打开网页前台。");
        }
    }

    /// <summary>
    /// 向日志框追加一行文本，自动切回 UI 线程。
    /// </summary>
    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), message);
            return;
        }

        string line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        _logBox.AppendText(line);
        _logBox.ScrollToCaret();
    }

    /// <summary>
    /// 更新指定流程卡片的状态颜色。
    /// </summary>
    private void SetStepState(string stepName, StepState state)
    {
        if (!_stepLabels.TryGetValue(stepName, out Label? label))
        {
            return;
        }

        label.Text = stepName + Environment.NewLine + GetStepStateText(state);
        label.BackColor = GetStepStateColor(state);
    }

    /// <summary>
    /// 创建流程卡片。
    /// </summary>
    private void AddStepCard(FlowLayoutPanel panel, string stepName)
    {
        Label label = new Label
        {
            Text = stepName + Environment.NewLine + "待处理",
            Width = 142,
            Height = 58,
            Margin = new Padding(0, 0, 10, 10),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
            BackColor = GetStepStateColor(StepState.Pending),
            ForeColor = Color.FromArgb(15, 23, 42)
        };

        _stepLabels[stepName] = label;
        panel.Controls.Add(label);
    }

    /// <summary>
    /// 创建卡片背景面板。
    /// </summary>
    private Panel CreateCardPanel()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            BackColor = Color.White
        };
    }

    /// <summary>
    /// 创建区域标题。
    /// </summary>
    private Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42)
        };
    }

    /// <summary>
    /// 创建输入框标签。
    /// </summary>
    private Label CreateInputLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(51, 65, 85)
        };
    }

    /// <summary>
    /// 创建配置输入框。
    /// </summary>
    private TextBox CreateInputBox(int x, int y, bool usePasswordChar)
    {
        return new TextBox
        {
            Location = new Point(x, y),
            Width = 302,
            Height = 30,
            Font = new Font("Microsoft YaHei UI", 10),
            UseSystemPasswordChar = usePasswordChar
        };
    }

    /// <summary>
    /// 创建主要操作按钮。
    /// </summary>
    private MaterialButton CreateActionButton(string text, int x, int y, Color backColor)
    {
        MaterialButton button = new MaterialButton
        {
            Text = text,
            Location = new Point(x, y),
            Width = 302,
            Height = 40,
            AutoSize = false,
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = backColor,
            Cursor = Cursors.Hand
        };

        button.Type = MaterialButton.MaterialButtonType.Contained;
        button.HighEmphasis = true;
        button.UseAccentColor = false;
        return button;
    }

    /// <summary>
    /// 创建顶部端口状态标签。
    /// </summary>
    private Label CreateStatusBadge(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            Width = 136,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 8, FontStyle.Bold),
            BackColor = Color.FromArgb(254, 243, 199),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
    }

    /// <summary>
    /// 获取流程状态文字。
    /// </summary>
    private static string GetStepStateText(StepState state)
    {
        return state switch
        {
            StepState.Running => "处理中",
            StepState.Success => "完成",
            StepState.Warning => "需处理",
            StepState.Failed => "失败",
            _ => "待处理"
        };
    }

    /// <summary>
    /// 获取流程状态颜色。
    /// </summary>
    private static Color GetStepStateColor(StepState state)
    {
        return state switch
        {
            StepState.Running => Color.FromArgb(219, 234, 254),
            StepState.Success => Color.FromArgb(220, 252, 231),
            StepState.Warning => Color.FromArgb(254, 243, 199),
            StepState.Failed => Color.FromArgb(254, 226, 226),
            _ => Color.FromArgb(241, 245, 249)
        };
    }
}
