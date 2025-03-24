using System.Runtime.InteropServices;
using ProtoBuf;

namespace AudioRecorder.Core.Data;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct InputAudioDeviceInfo
{
    public AudioDeviceInfo DeviceInfo;
}

[ProtoContract]
internal sealed class InputAudioDevice : AudioDevice
{
    private InputAudioDevice() { }

    public InputAudioDevice(InputAudioDeviceInfo deviceInfo) : base(deviceInfo.DeviceInfo) { }
}
