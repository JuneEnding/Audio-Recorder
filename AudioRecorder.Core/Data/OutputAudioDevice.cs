using System.Runtime.InteropServices;
using ProtoBuf;

namespace AudioRecorder.Core.Data;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct OutputAudioDeviceInfo
{
    public AudioDeviceInfo DeviceInfo;
    public IntPtr Sessions;
    public int SessionCount;
}

[ProtoContract]
internal sealed class OutputAudioDevice : AudioDevice
{
    private OutputAudioDevice()
    {
        AudioSessions = [];
    }

    public OutputAudioDevice(OutputAudioDeviceInfo outputDeviceInfo) : base(outputDeviceInfo.DeviceInfo)
    {
        AudioSessions = [.. AudioSession.FromOutputAudioDeviceInfo(outputDeviceInfo)];
    }

    public OutputAudioDevice(AudioDeviceInfo deviceInfo, IEnumerable<AudioSession> sessions) : base(deviceInfo)
    {
        AudioSessions = [.. sessions];
    }

    [ProtoMember(3)]
    public RangedObservableCollection<AudioSession> AudioSessions { get; }
}
