using System.Runtime.InteropServices;

namespace AudioRecorder.Core.Services;

public sealed class LoggerInterop
{
    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void set_logger(IntPtr logger);

    public static void InitializeCppLogger()
    {
        var logDelegate = new Logger.LogDelegate(Logger.LogFromCpp);
        var logPtr = Marshal.GetFunctionPointerForDelegate(logDelegate);
        set_logger(logPtr);
    }
}