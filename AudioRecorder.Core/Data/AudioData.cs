using System.IO.Pipes;

namespace AudioRecorder.Core.Data;

public enum AudioTargetType
{
    Process = 0,
    AudioDevice
}

public sealed class AudioData
{
    public NamedPipeClientStream? PipeClient { get; set; }
    public List<byte> Buffer { get; set; } = new();
    public uint PipeId { get; set; }
    public long CaptureId { get; set; }
    public uint SampleRate { get; set; } = 44100;
    public ushort BitsPerSample { get; set; } = 16;
    public ushort Channels { get; set; } = 2;
    public Thread? ProcessingThread { get; set; }
    public bool CancelRequested { get; set; }
    public string Name { get; set; } = string.Empty;
    public AudioTargetType Type { get; set; }

    public AudioData(AudioTargetType type)
    {
        Type = type;
    }

    public AudioData(NativeAudioDeviceInfo deviceInfo, AudioTargetType type)
    {
        PipeId = deviceInfo.PipeId;
        SampleRate = deviceInfo.SampleRate;
        BitsPerSample = deviceInfo.BitsPerSample;
        Channels = deviceInfo.Channels;
        Name = deviceInfo.Name;
        Type = type;
    }
}