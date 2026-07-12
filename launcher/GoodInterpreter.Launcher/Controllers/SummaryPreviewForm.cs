namespace GoodInterpreter.Launcher.Controllers;

/// <summary>
/// AI 会议总结预览窗口，支持复制和另存为 TXT。
/// </summary>
public sealed class SummaryPreviewForm : Form
{
    /// <summary>
    /// 总结内容文本框。
    /// </summary>
    private readonly TextBox _summaryTextBox = new TextBox();

    /// <summary>
    /// 创建总结预览窗口。
    /// </summary>
    public SummaryPreviewForm(string summaryText)
    {
        Text = "总结会议纪要";
        Size = new Size(720, 560);
        MinimumSize = new Size(520, 360);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);

        BuildLayout(summaryText);
    }

    /// <summary>
    /// 创建预览和操作按钮布局。
    /// </summary>
    private void BuildLayout(string summaryText)
    {
        TableLayoutPanel rootPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14),
            BackColor = BackColor
        };

        rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        _summaryTextBox.Multiline = true;
        _summaryTextBox.ScrollBars = ScrollBars.Vertical;
        _summaryTextBox.ReadOnly = true;
        _summaryTextBox.Dock = DockStyle.Fill;
        _summaryTextBox.BorderStyle = BorderStyle.FixedSingle;
        _summaryTextBox.Font = new Font("Microsoft YaHei UI", 10);
        _summaryTextBox.Text = summaryText;

        FlowLayoutPanel buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent
        };

        Button closeButton = CreateButton("关闭");
        closeButton.Click += (_, _) => Close();

        Button saveButton = CreateButton("保存 TXT");
        saveButton.Click += (_, _) => SaveSummary();

        Button copyButton = CreateButton("复制");
        copyButton.Click += (_, _) => Clipboard.SetText(_summaryTextBox.Text);

        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(copyButton);

        rootPanel.Controls.Add(_summaryTextBox, 0, 0);
        rootPanel.Controls.Add(buttonPanel, 0, 1);
        Controls.Add(rootPanel);
    }

    /// <summary>
    /// 创建统一尺寸的操作按钮。
    /// </summary>
    private static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 96,
            Height = 34,
            Margin = new Padding(8, 8, 0, 0),
            Font = new Font("Microsoft YaHei UI", 9),
        };
    }

    /// <summary>
    /// 保存 AI 总结为 TXT。
    /// </summary>
    private void SaveSummary()
    {
        using SaveFileDialog dialog = new SaveFileDialog
        {
            Title = "保存总结会议纪要",
            Filter = "文本文件 (*.txt)|*.txt",
            FileName = $"总结会议纪要-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, _summaryTextBox.Text, System.Text.Encoding.UTF8);
        }
    }
}
