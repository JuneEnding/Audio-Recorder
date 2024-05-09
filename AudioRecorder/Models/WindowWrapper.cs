using Avalonia.Media.Imaging;
using System;

namespace AudioRecorder.Models
{
    public class WindowWrapper
    {
        internal IntPtr handle;
        internal string title = "";
        internal int processId;
        internal Bitmap? icon;

        public IntPtr Handle { get => handle; }
        public string Title { get => title; }
        public int ProcessId { get => processId; }
        public Bitmap? Icon { get => icon; }
    }
}
