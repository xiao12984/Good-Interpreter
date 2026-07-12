using NAudio.Wave;

namespace GoodInterpreter.Launcher.Services;

/// <summary>
/// 音频格式转换工具，将 WASAPI 原始音频转换为火山 AST 需要的 16k 单声道 16-bit PCM。
/// </summary>
public static class PcmAudioConverter
{
    /// <summary>
    /// AST 目标采样率。
    /// </summary>
    public const int TargetSampleRate = 16000;

    /// <summary>
    /// 将任意常见 WASAPI PCM/Float 缓冲转换为 16k 单声道 PCM。
    /// </summary>
    public static byte[] ConvertToTargetPcm(byte[] buffer, int bytesRecorded, WaveFormat sourceFormat)
    {
        if (bytesRecorded <= 0 || sourceFormat.Channels <= 0 || sourceFormat.BlockAlign <= 0)
        {
            return Array.Empty<byte>();
        }

        int frameCount = bytesRecorded / sourceFormat.BlockAlign;
        if (frameCount <= 0)
        {
            return Array.Empty<byte>();
        }

        float[] monoSamples = ExtractStrongestMonoChannel(buffer, frameCount, sourceFormat);
        if (monoSamples.Length == 0)
        {
            return Array.Empty<byte>();
        }

        float[] resampledSamples = ResampleLinear(monoSamples, sourceFormat.SampleRate, TargetSampleRate);
        return ConvertFloatToInt16Bytes(resampledSamples);
    }

    /// <summary>
    /// 生成流式 WAV 头，麦克风模式发送给 AST 以匹配后端 wav 协议。
    /// </summary>
    public static byte[] CreateStreamingWavHeader()
    {
        byte[] header = new byte[44];
        using MemoryStream stream = new MemoryStream(header);
        using BinaryWriter writer = new BinaryWriter(stream);

        int byteRate = TargetSampleRate * 1 * 16 / 8;
        short blockAlign = 1 * 16 / 8;

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(uint.MaxValue);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(TargetSampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(uint.MaxValue);

        return header;
    }

    /// <summary>
    /// 从多声道输入中选择能量最强的声道，避免左右声道相位抵消导致语音变弱。
    /// </summary>
    private static float[] ExtractStrongestMonoChannel(byte[] buffer, int frameCount, WaveFormat format)
    {
        int channels = format.Channels;
        float[] interleavedSamples = new float[frameCount * channels];
        double[] channelEnergy = new double[channels];

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            for (int channelIndex = 0; channelIndex < channels; channelIndex++)
            {
                int sampleIndex = frameIndex * channels + channelIndex;
                float sample = ReadSample(buffer, sampleIndex, format);
                interleavedSamples[sampleIndex] = sample;
                channelEnergy[channelIndex] += sample * sample;
            }
        }

        int strongestChannel = 0;
        for (int channelIndex = 1; channelIndex < channels; channelIndex++)
        {
            if (channelEnergy[channelIndex] > channelEnergy[strongestChannel])
            {
                strongestChannel = channelIndex;
            }
        }

        float[] monoSamples = new float[frameCount];
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            monoSamples[frameIndex] = interleavedSamples[frameIndex * channels + strongestChannel];
        }

        return monoSamples;
    }

    /// <summary>
    /// 读取一个 PCM 或 IEEE float 采样，并归一化到 -1 到 1。
    /// </summary>
    private static float ReadSample(byte[] buffer, int sampleIndex, WaveFormat format)
    {
        int bytesPerSample = Math.Max(format.BitsPerSample / 8, 1);
        int offset = sampleIndex * bytesPerSample;

        if (offset + bytesPerSample > buffer.Length)
        {
            return 0;
        }

        bool isFloatFormat = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            (format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32);
        bool isPcmFormat = format.Encoding == WaveFormatEncoding.Pcm ||
            format.Encoding == WaveFormatEncoding.Extensible;

        if (isFloatFormat)
        {
            return Math.Clamp(BitConverter.ToSingle(buffer, offset), -1.0f, 1.0f);
        }

        if (!isPcmFormat)
        {
            return 0;
        }

        return format.BitsPerSample switch
        {
            16 => BitConverter.ToInt16(buffer, offset) / 32768.0f,
            24 => ReadInt24(buffer, offset) / 8388608.0f,
            32 => BitConverter.ToInt32(buffer, offset) / 2147483648.0f,
            _ => 0
        };
    }

    /// <summary>
    /// 读取带符号 24-bit PCM。
    /// </summary>
    private static int ReadInt24(byte[] buffer, int offset)
    {
        int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);

        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }

    /// <summary>
    /// 使用线性插值重采样，足够覆盖实时语音输入。
    /// </summary>
    private static float[] ResampleLinear(float[] inputSamples, int sourceSampleRate, int targetSampleRate)
    {
        if (sourceSampleRate == targetSampleRate)
        {
            return inputSamples;
        }

        double ratio = (double)sourceSampleRate / targetSampleRate;
        int outputLength = Math.Max(1, (int)Math.Round(inputSamples.Length / ratio));
        float[] outputSamples = new float[outputLength];

        for (int outputIndex = 0; outputIndex < outputLength; outputIndex++)
        {
            double sourceIndex = outputIndex * ratio;
            int leftIndex = (int)Math.Floor(sourceIndex);
            int rightIndex = Math.Min(leftIndex + 1, inputSamples.Length - 1);
            float weight = (float)(sourceIndex - leftIndex);

            outputSamples[outputIndex] =
                inputSamples[leftIndex] * (1 - weight) +
                inputSamples[rightIndex] * weight;
        }

        return outputSamples;
    }

    /// <summary>
    /// 将 float 采样转换为 little-endian 16-bit PCM 字节。
    /// </summary>
    private static byte[] ConvertFloatToInt16Bytes(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];

        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
        {
            short value = (short)Math.Clamp(samples[sampleIndex] * short.MaxValue, short.MinValue, short.MaxValue);
            BitConverter.GetBytes(value).CopyTo(bytes, sampleIndex * 2);
        }

        return bytes;
    }
}
