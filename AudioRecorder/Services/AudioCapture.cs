using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioRecorder.Services
{
    public static class AudioCapture
    {
        [DllImport("ApplicationLoopbackLibrary.dll", CharSet = CharSet.Ansi)]
        public static extern void StartCapture([In] int[] pids, int count);

        [DllImport("ApplicationLoopbackLibrary.dll", CharSet = CharSet.Ansi)]
        public static extern void StopCapture([In] int[] pids, int count);

        public static void StartCapture(List<int> pids) =>
            StartCapture(pids.ToArray(), pids.Count);

        public static void StopCapture(List<int> pids) =>
            StopCapture(pids.ToArray(), pids.Count);
    }
}
