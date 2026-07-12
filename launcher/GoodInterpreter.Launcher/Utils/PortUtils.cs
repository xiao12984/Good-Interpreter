using System.Net.NetworkInformation;

namespace GoodInterpreter.Launcher.Utils;

/// <summary>
/// 端口检查工具，用于判断本机服务端口是否已经监听。
/// </summary>
public static class PortUtils
{
    /// <summary>
    /// 判断本机指定端口是否已有监听进程。
    /// </summary>
    public static bool IsListening(int port)
    {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

        return properties
            .GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == port);
    }
}
