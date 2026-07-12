using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// 统一封装外部进程启动、日志捕获和停止逻辑，界面层不直接操作 Process。
/// </summary>
public sealed class ProcessRunner
{
    /// <summary>
    /// 匹配终端 ANSI 控制码，例如彩色日志中的 ESC[32m，避免日志框显示乱码字符。
    /// </summary>
    private static readonly Regex AnsiControlCodeRegex = new Regex(
        @"\x1B(?:\[[0-?]*[ -/]*[@-~]|\][^\x07]*(?:\x07|\x1B\\))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 外部进程输出事件，界面通过此事件实时显示日志。
    /// </summary>
    public event Action<string>? OutputReceived;

    /// <summary>
    /// 启动长期运行的内置服务进程。
    /// </summary>
    public Process StartServiceProcess(string fileName, string arguments, string workingDirectory, string displayName)
    {
        Process process = CreateProcess(fileName, arguments, workingDirectory);

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => OutputReceived?.Invoke($"[{displayName}] 进程已退出，退出码：{process.ExitCode}");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        OutputReceived?.Invoke($"[{displayName}] 已启动，PID：{process.Id}");
        return process;
    }

    /// <summary>
    /// 停止由启动器创建的进程，并尽量连同子进程一起结束。
    /// </summary>
    public void StopProcess(Process? process, string displayName)
    {
        if (process == null || process.HasExited)
        {
            OutputReceived?.Invoke($"[{displayName}] 未运行，无需停止。");
            return;
        }

        process.Kill(entireProcessTree: true);
        OutputReceived?.Invoke($"[{displayName}] 已请求停止。");
    }

    /// <summary>
    /// 创建带输出重定向的进程对象。
    /// </summary>
    private Process CreateProcess(string fileName, string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        ConfigureUtf8Environment(startInfo);

        Process process = new Process
        {
            StartInfo = startInfo
        };

        process.OutputDataReceived += (_, eventArgs) => PublishOutput(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => PublishOutput(eventArgs.Data);

        return process;
    }

    /// <summary>
    /// 给后端服务进程设置 UTF-8 环境，避免中文日志被错误解码。
    /// </summary>
    private static void ConfigureUtf8Environment(ProcessStartInfo startInfo)
    {
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["LANG"] = "zh_CN.UTF-8";
        startInfo.Environment["LC_ALL"] = "zh_CN.UTF-8";

        // 禁用常见终端工具的彩色控制码输出，避免 RichTextBox 直接显示 ESC[32m 这类终端序列。
        startInfo.Environment["NO_COLOR"] = "1";
        startInfo.Environment["FORCE_COLOR"] = "0";
        startInfo.Environment["TERM"] = "dumb";
    }

    /// <summary>
    /// 过滤空输出和 ANSI 控制码，减少日志噪音并保证界面显示纯文本。
    /// </summary>
    private void PublishOutput(string? line)
    {
        string cleanLine = StripAnsiControlCodes(line);

        if (!string.IsNullOrWhiteSpace(cleanLine))
        {
            OutputReceived?.Invoke(cleanLine);
        }
    }

    /// <summary>
    /// 清理终端颜色、光标等 ANSI 控制码，保留真正的日志文本。
    /// </summary>
    private static string StripAnsiControlCodes(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        return AnsiControlCodeRegex.Replace(line, string.Empty);
    }
}
