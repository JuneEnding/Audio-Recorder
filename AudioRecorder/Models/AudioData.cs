using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;

namespace AudioRecorder.Models
{
    public class AudioData
    {
        public NamedPipeClientStream? PipeClient { get; set; }
        public List<byte> Buffer { get; set; } = new();
        public int Pid { get; set; }
        public long CaptureId { get; set; }
        public Thread? ProcessingThread { get; set; }
        public bool CancelRequested { get; set; }
    }
}
