using ProtoBuf;

namespace AudioRecorder.Core.Data;

[ProtoContract]
internal sealed class AudioStateWrapper
{
    [ProtoMember(1)] public List<InputAudioDevice> InputDevices { get; set; } = [];
    [ProtoMember(2)] public List<OutputAudioDevice> OutputDevices { get; set; } = [];
}