using System.Diagnostics;
using System.Runtime.InteropServices;
using ProtoBuf;
using ReactiveUI;

namespace AudioRecorder.Core.Data;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct AudioDeviceInfo
{
    [MarshalAs(UnmanagedType.BStr)]
    public string Id;
    [MarshalAs(UnmanagedType.BStr)]
    public string Name;
    public uint SampleRate;
    public ushort BitsPerSample;
    public ushort Channels;
}

[ProtoContract]
[ProtoInclude(100, typeof(InputAudioDevice))]
[ProtoInclude(101, typeof(OutputAudioDevice))]
internal class AudioDevice : ReactiveObject
{
    [ProtoMember(1)]
    public string Id { get; }
    [ProtoIgnore]
    public uint SampleRate { get; }
    [ProtoIgnore]
    public ushort BitsPerSample { get; }
    [ProtoIgnore]
    public ushort Channels { get; }
    [ProtoIgnore]
    private string _name = string.Empty;
    [ProtoIgnore]
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }
    [ProtoIgnore]
    private bool _isChecked;
    [ProtoMember(2)]
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            //try
            //{
            //    Console.WriteLine($"Started is checked {value}");
                this.RaiseAndSetIfChanged(ref _isChecked, value);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine(ex.Message);
            //}
        }
    }

    public AudioDevice(AudioDeviceInfo deviceInfo)
    {
        Id = deviceInfo.Id;
        Name = string.IsNullOrEmpty(deviceInfo.Name) ? "Unknown" : deviceInfo.Name;
        SampleRate = deviceInfo.SampleRate;
        BitsPerSample = deviceInfo.BitsPerSample;
        Channels = deviceInfo.Channels;
    }

    protected AudioDevice()
    {
        Id = "";
    }

    public AudioDeviceInfo ToAudioDeviceInfo() =>
        new()
        {
            Id = Id,
            Name = Name,
            SampleRate = SampleRate,
            BitsPerSample = BitsPerSample,
            Channels = Channels
        };
}