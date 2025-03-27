using System.Runtime.InteropServices;

namespace AudioRecorder.Core.Services;

internal static class AudioDataInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void AudioDataCallback(long captureId, IntPtr sourceId, IntPtr data, int length);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void set_audio_data_callback(IntPtr callback);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void clear_audio_data_callback();

    private static AudioDataCallback? _managedCallback;

    public static void SetAudioDataCallback(AudioDataCallback callback)
    {
        _managedCallback = callback;

        var funcPtr = Marshal.GetFunctionPointerForDelegate(_managedCallback);

        set_audio_data_callback(funcPtr);
    }

    public static void UnsetAudioDataCallback()
    {
        clear_audio_data_callback();
        _managedCallback = null;
    }
}