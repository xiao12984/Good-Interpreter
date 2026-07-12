using GoodInterpreter.Launcher.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// Windows 原生音频采集服务，负责麦克风和系统音频 loopback 输入。
/// </summary>
public sealed class NativeAudioCaptureService : IDisposable
{
    /// <summary>
    /// 当前 WASAPI 采集实例。
    /// </summary>
    private WasapiCapture? _capture;

    /// <summary>
    /// 设备枚举器。
    /// </summary>
    private readonly MMDeviceEnumerator _deviceEnumerator = new MMDeviceEnumerator();

    /// <summary>
    /// 采集到一段已经转换好的 16k 单声道 PCM 音频。
    /// </summary>
    public event Action<byte[]>? AudioDataCaptured;

    /// <summary>
    /// 采集错误或停止提示。
    /// </summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// 当前是否正在采集。
    /// </summary>
    public bool IsRecording => _capture != null;

    /// <summary>
    /// 枚举指定模式下可用的音频设备。
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> GetDevices(AudioInputMode mode)
    {
        DataFlow dataFlow = mode == AudioInputMode.Microphone
            ? DataFlow.Capture
            : DataFlow.Render;

        return _deviceEnumerator
            .EnumerateAudioEndPoints(dataFlow, DeviceState.Active)
            .Select(device => new AudioDeviceInfo(device.ID, device.FriendlyName, mode))
            .ToList();
    }

    /// <summary>
    /// 开始采集指定设备。
    /// </summary>
    public void Start(AudioInputMode mode, string deviceId)
    {
        Stop();

        MMDevice device = FindDevice(mode, deviceId);
        if (mode == AudioInputMode.Microphone)
        {
            _capture = new WasapiCapture(device);
        }
        else
        {
            _capture = new WasapiLoopbackCapture(device);
        }

        _capture.DataAvailable += HandleDataAvailable;
        _capture.RecordingStopped += HandleRecordingStopped;
        _capture.StartRecording();

        StatusChanged?.Invoke("音频采集已启动。");
    }

    /// <summary>
    /// 停止采集并释放设备。
    /// </summary>
    public void Stop()
    {
        if (_capture == null)
        {
            return;
        }

        WasapiCapture capture = _capture;
        _capture = null;

        capture.DataAvailable -= HandleDataAvailable;
        capture.RecordingStopped -= HandleRecordingStopped;
        try
        {
            // StopRecording 在设备热插拔或窗口快速关闭时可能抛出底层 COM 异常，停止路径直接忽略。
            capture.StopRecording();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("音频采集停止提示：" + ex.Message);
        }
        finally
        {
            capture.Dispose();
        }
    }

    /// <summary>
    /// 找到用户选择的设备；如果设备不存在则使用当前模式的默认设备。
    /// </summary>
    private MMDevice FindDevice(AudioInputMode mode, string deviceId)
    {
        DataFlow dataFlow = mode == AudioInputMode.Microphone
            ? DataFlow.Capture
            : DataFlow.Render;

        MMDeviceCollection devices = _deviceEnumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active);
        foreach (MMDevice device in devices)
        {
            if (string.Equals(device.ID, deviceId, StringComparison.Ordinal))
            {
                return device;
            }
        }

        return _deviceEnumerator.GetDefaultAudioEndpoint(dataFlow, Role.Console);
    }

    /// <summary>
    /// 将 WASAPI 回调中的音频转换后抛给翻译 WebSocket。
    /// </summary>
    private void HandleDataAvailable(object? sender, WaveInEventArgs eventArgs)
    {
        WasapiCapture? capture = _capture;
        if (capture == null)
        {
            return;
        }

        byte[] pcmData = PcmAudioConverter.ConvertToTargetPcm(
            eventArgs.Buffer,
            eventArgs.BytesRecorded,
            capture.WaveFormat);

        if (pcmData.Length > 0)
        {
            AudioDataCaptured?.Invoke(pcmData);
        }
    }

    /// <summary>
    /// 捕获底层采集停止事件，向界面展示异常信息。
    /// </summary>
    private void HandleRecordingStopped(object? sender, StoppedEventArgs eventArgs)
    {
        if (eventArgs.Exception != null)
        {
            StatusChanged?.Invoke("音频采集异常：" + eventArgs.Exception.Message);
        }
    }

    /// <summary>
    /// 释放设备枚举器和采集实例。
    /// </summary>
    public void Dispose()
    {
        Stop();
        _deviceEnumerator.Dispose();
    }
}
