using System.Runtime.InteropServices;
using AudioRecorder.Core.Data;

namespace AudioRecorder.Core.Services;

internal static class AudioCaptureService
{
    public static long StartCapture(AudioDeviceInfo[] inputDevices, AudioDeviceInfo[] outputDevices, AudioSessionInfo[] sessions)
    {
        return StartCapture(inputDevices, inputDevices.Length, outputDevices, outputDevices.Length, sessions, sessions.Length);
    }

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern void StopCapture(long captureId);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern long StartCapture([In] AudioDeviceInfo[] inputDevices, int inputDeviceCount,
        [In] AudioDeviceInfo[] outputDevices, int outputDeviceCount, [In] AudioSessionInfo[] sessions,
        int sessionCount);
}