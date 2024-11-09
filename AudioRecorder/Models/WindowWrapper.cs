using Avalonia.Media.Imaging;
using System;

namespace AudioRecorder.Models
{
    public class WindowWrapper
    {
        internal IntPtr handle;
        internal string title = "";
        internal uint processId;
        internal Bitmap? icon;

        public IntPtr Handle { get => handle; }
        public string Title { get => title; }
        public uint ProcessId { get => processId; }
        public Bitmap? Icon { get => icon; }
    }
}
