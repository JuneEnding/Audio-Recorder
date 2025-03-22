using System.Runtime.InteropServices;

namespace AudioRecorder.Core.Data;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct OutputAudioDeviceInfo
{
    public AudioDeviceInfo DeviceInfo;
    public IntPtr Sessions;
    public int SessionCount;
}

internal sealed class OutputAudioDevice(OutputAudioDeviceInfo deviceInfo) : AudioDevice(deviceInfo.DeviceInfo)
{
    public readonly RangedObservableCollection<AudioSession> AudioSessions = [.. AudioSession.FromOutputAudioDeviceInfo(deviceInfo)];
}
