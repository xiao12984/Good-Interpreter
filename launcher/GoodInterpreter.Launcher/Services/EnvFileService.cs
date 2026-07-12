using System.Text;
using GoodInterpreter.Launcher.Config;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// 负责读取和保存后端 .env 配置，避免用户手动编辑文本文件。
/// </summary>
public sealed class EnvFileService
{
    private readonly AppPaths _paths;

    /// <summary>
    /// 创建 .env 服务。
    /// </summary>
    public EnvFileService(AppPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 读取 .env 中的键值对，空文件或不存在时返回空字典。
    /// </summary>
    public Dictionary<string, string> LoadValues()
    {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_paths.EnvFilePath))
        {
            return values;
        }

        foreach (string line in File.ReadAllLines(_paths.EnvFilePath, Encoding.UTF8))
        {
            string trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string key = trimmed[..separatorIndex].Trim();
            string value = trimmed[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }

    /// <summary>
    /// 保存火山引擎密钥和服务端口，自动保持前端代理需要的 3100 端口。
    /// </summary>
    public void SaveVolcengineSettings(string appId, string accessKey)
    {
        Dictionary<string, string> values = LoadValues();
        string openAiKey = values.TryGetValue("OPENAI_API_KEY", out string? existingOpenAiKey)
            ? existingOpenAiKey
            : string.Empty;
        string resourceId = values.TryGetValue("VOLC_RESOURCE_ID", out string? existingResourceId)
            ? existingResourceId
            : "volc.service_type.10053";

        string content = string.Join(Environment.NewLine, new[]
        {
            "# Volcengine API Credentials",
            $"VOLC_APP_ID={appId.Trim()}",
            $"VOLC_ACCESS_KEY={accessKey.Trim()}",
            string.Empty,
            "# Optional OpenAI key, only used when clicking meeting summary",
            $"OPENAI_API_KEY={openAiKey}",
            string.Empty,
            "# Volcengine AST resource id, keep default unless console gives another one",
            $"VOLC_RESOURCE_ID={resourceId}",
            string.Empty,
            "# Server Configuration",
            $"PORT={LauncherConstants.BackendPort}",
            "HOST=0.0.0.0",
            string.Empty,
            "# Debug Mode",
            "DEBUG=false",
            string.Empty
        });

        Directory.CreateDirectory(_paths.BackendPath);
        File.WriteAllText(_paths.EnvFilePath, content, Encoding.UTF8);
    }

    /// <summary>
    /// 判断火山引擎配置是否已经填写为真实值。
    /// </summary>
    public bool HasValidVolcengineKeys()
    {
        Dictionary<string, string> values = LoadValues();

        return IsRealValue(values, "VOLC_APP_ID") && IsRealValue(values, "VOLC_ACCESS_KEY");
    }

    /// <summary>
    /// 读取指定键并判断是否不是占位符。
    /// </summary>
    private static bool IsRealValue(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string? value))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(value)
            && !value.Contains("your_", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("你的", StringComparison.OrdinalIgnoreCase);
    }
}
