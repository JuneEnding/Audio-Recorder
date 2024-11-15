using System.Runtime.InteropServices;
using Serilog;

namespace AudioRecorder.Core.Services;

public static class Logger
{
    private const string LogFilePath = "Logs/log.txt";

    static Logger()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(LogFilePath, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    public static void LogInfo(string message) => Log.Information(message);
    public static void LogWarning(string message) => Log.Warning(message);
    public static void LogError(string message) => Log.Error(message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogDelegate(string message, int level);

    public static void LogFromCpp(string message, int level)
    {
        switch (level)
        {
            case 0: LogInfo(message); break;
            case 1: LogWarning(message); break;
            case 2: LogError(message); break;
            default: LogInfo(message); break;
        }
    }
}