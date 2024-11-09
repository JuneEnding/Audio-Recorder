using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using AudioRecorder.Services;

namespace AudioRecorder.Models
{
    public class AudioData
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

        public AudioData() { }

        public AudioData(AudioCapture.NativeAudioDeviceInfo deviceInfo)
        {
            PipeId = deviceInfo.PipeId;
            SampleRate = deviceInfo.SampleRate;
            BitsPerSample = deviceInfo.BitsPerSample;
            Channels = deviceInfo.Channels;
        }
    }
}
