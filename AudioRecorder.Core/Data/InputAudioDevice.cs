using System.Runtime.InteropServices;

namespace AudioRecorder.Core.Data;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct InputAudioDeviceInfo
{
    public AudioDeviceInfo DeviceInfo;
}

internal sealed class InputAudioDevice(AudioDeviceInfo deviceInfo) : AudioDevice(deviceInfo)
{
}
