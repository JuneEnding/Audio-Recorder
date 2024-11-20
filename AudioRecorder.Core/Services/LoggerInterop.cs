using System;
using System.Runtime.InteropServices;

namespace AudioRecorder.Core.Services;

public static class LoggerInterop
{
    private static Logger.LogDelegate? _logDelegate;

    public static void InitializeCppLogger()
    {
        _logDelegate = Logger.LogFromCpp;
        var logPtr = Marshal.GetFunctionPointerForDelegate(_logDelegate);
        set_logger(logPtr);
    }

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void set_logger(IntPtr logger);
}
