using System.Runtime.InteropServices;
using ReactiveUI;

namespace AudioRecorder.Core.Data;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct AudioDeviceInfo
{
    public uint PipeId;
    [MarshalAs(UnmanagedType.BStr)]
    public string Id;
    [MarshalAs(UnmanagedType.BStr)]
    public string Name;
    public uint SampleRate;
    public ushort BitsPerSample;
    public ushort Channels;
}

internal class AudioDevice : ReactiveObject
{
    public uint PipeId { get; }
    public string Id { get; }
    public uint SampleRate { get; }
    public ushort BitsPerSample { get; }
    public ushort Channels { get; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }

    public AudioDevice(AudioDeviceInfo deviceInfo)
    {
        PipeId = deviceInfo.PipeId;
        Id = deviceInfo.Id;
        Name = string.IsNullOrEmpty(deviceInfo.Name) ? "Unknown" : deviceInfo.Name;
        SampleRate = deviceInfo.SampleRate;
        BitsPerSample = deviceInfo.BitsPerSample;
        Channels = deviceInfo.Channels;
    }

    public AudioDeviceInfo ToAudioDeviceInfo() =>
        new()
        {
            PipeId = PipeId,
            Id = Id,
            Name = Name,
            SampleRate = SampleRate,
            BitsPerSample = BitsPerSample,
            Channels = Channels
        };
}