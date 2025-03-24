using AudioRecorder.Core.Services;
using ReactiveUI;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using DynamicData;
using ProtoBuf;

namespace AudioRecorder.Core.Data;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct AudioSessionInfo
{
    public uint PipeId;
    [MarshalAs(UnmanagedType.BStr)]
    public string DisplayName;
    [MarshalAs(UnmanagedType.BStr)]
    public string IconPath;
    public bool IsSystemSession;
    [MarshalAs(UnmanagedType.BStr)]
    public string SessionIdentifier;
    [MarshalAs(UnmanagedType.BStr)]
    public string SessionInstanceIdentifier;
}

[ProtoContract]
internal sealed class AudioSession : ReactiveObject
{
    [ProtoMember(1)]
    public string SessionId { get; }
    [ProtoIgnore]
    public string SessionInstanceIdentifier { get; }
    [ProtoIgnore]
    public uint PipeId { get; }
    [ProtoIgnore]
    public uint ProcessId => PipeId;
    [ProtoIgnore]
    public Bitmap? Icon { get; }
    [ProtoIgnore]
    public uint SampleRate { get; }
    [ProtoIgnore]
    public ushort BitsPerSample { get; }
    [ProtoIgnore]
    public ushort Channels { get; }
    [ProtoIgnore]

    private string _displayName = string.Empty;
    [ProtoIgnore]
    public string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    [ProtoIgnore]
    private bool _isChecked;
    [ProtoMember(2)]
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }

    public AudioSession(AudioDeviceInfo deviceInfo, AudioSessionInfo sessionInfo)
    {
        SessionId = sessionInfo.SessionIdentifier;
        SessionInstanceIdentifier = sessionInfo.SessionInstanceIdentifier;
        PipeId = sessionInfo.PipeId;
        DisplayName = sessionInfo.DisplayName;
        SampleRate = deviceInfo.SampleRate;
        BitsPerSample = deviceInfo.BitsPerSample;
        Channels = deviceInfo.Channels;

        Icon = ResourceHelper.GetBitmapFromIconPath(sessionInfo.IconPath);
        Icon ??= NativeMethods.GetIconForProcess(ProcessId);
    }

    private AudioSession()
    {
        SessionId = "";
        SessionInstanceIdentifier = "";
    }

    public static IEnumerable<AudioSession> FromOutputAudioDeviceInfo(OutputAudioDeviceInfo outputDevice)
    {
        var audioSessions = new AudioSession[outputDevice.SessionCount];

        if (outputDevice.Sessions == IntPtr.Zero || outputDevice.SessionCount <= 0) return audioSessions;

        for (var i = 0; i < outputDevice.SessionCount; ++i)
        {
            var sessionPtr = IntPtr.Add(outputDevice.Sessions, i * Marshal.SizeOf<AudioSessionInfo>());
            var sessionStruct = Marshal.PtrToStructure<AudioSessionInfo>(sessionPtr);
            audioSessions[i] = new AudioSession(outputDevice.DeviceInfo, sessionStruct);
        }

        return audioSessions;
    }

    public AudioSessionInfo ToAudioSessionInfo() =>
        new()
        {
            PipeId = PipeId,
            DisplayName = DisplayName,
            SessionInstanceIdentifier = SessionInstanceIdentifier,
            SessionIdentifier = SessionId
        };
}