using System.Diagnostics.CodeAnalysis;
using AudioRecorder.Core.Data;
using System.Runtime.InteropServices;
using ReactiveUI;

namespace AudioRecorder.Core.Services;

internal sealed class InputAudioDeviceService
{
    private const int DeviceStateActive = 0x00000001;
    private const int DeviceStateDisabled = 0x00000002;
    private const int DeviceStateUnplugged = 0x00000004;
    private const int DeviceStateNotPresent = 0x00000008;
    private const int DeviceStateMaskAll = 0x0000000F;

    [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
    private static DeviceStateChangedCallback? _deviceStateChangedCallback;

    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<InputAudioDeviceService> _instance = new(() => new InputAudioDeviceService());
    public static InputAudioDeviceService Instance => _instance.Value;

    public readonly RangedObservableCollection<InputAudioDevice> ActiveInputAudioDevices = new();

    public event Action<string, int>? DeviceStateChanged;

    private InputAudioDeviceService()
    {
        try
        {
            _deviceStateChangedCallback = OnDeviceStateChanged;
            RegisterInputNotificationCallback(_deviceStateChangedCallback);

            var devices = GetActiveAudioDevices().ToArray();
            ActiveInputAudioDevices.AddRange(devices);

            Logger.LogInfo("Listening for audio device changes...");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start listening for audio device changes: {ex}");
        }
    }

    ~InputAudioDeviceService()
    {
        UnregisterInputNotificationCallback();
    }

    private void OnDeviceStateChanged(string deviceId, int newState)
    {
        if (newState == DeviceStateActive)
        {
            var audioDeviceInfo = GetAudioDevice(deviceId);
            if (audioDeviceInfo == null)
                return;

            ActiveInputAudioDevices.Add(audioDeviceInfo);
        }
        else
        {
            var deviceToRemove = ActiveInputAudioDevices.FirstOrDefault(device => device.Id == deviceId);
            if (deviceToRemove == null)
                return;

            ActiveInputAudioDevices.Remove(deviceToRemove);
        }

        DeviceStateChanged?.Invoke(deviceId, newState);
    }

    private static IEnumerable<InputAudioDevice> GetActiveAudioDevices()
    {
        GetActiveInputAudioDevices(out var devicesPtr, out var count);

        var devices = new InputAudioDevice[count];
        if (count == 0 || devicesPtr == IntPtr.Zero)
            return devices;

        var structSize = Marshal.SizeOf<InputAudioDeviceInfo>();

        for (var i = 0; i < count; ++i)
        {
            var devicePtr = IntPtr.Add(devicesPtr, i * structSize);
            devices[i] = new InputAudioDevice(new InputAudioDeviceInfo
                { DeviceInfo = Marshal.PtrToStructure<InputAudioDeviceInfo>(devicePtr).DeviceInfo });
        }

        FreeInputAudioDevicesArray(devicesPtr, count);

        return devices;
    }

    private static InputAudioDevice? GetAudioDevice(string deviceId)
    {
        var audioDevicePtr = GetInputAudioDeviceInfo(deviceId);

        if (audioDevicePtr == IntPtr.Zero)
            return null;

        var device = new InputAudioDevice(new InputAudioDeviceInfo
            { DeviceInfo = Marshal.PtrToStructure<InputAudioDeviceInfo>(audioDevicePtr).DeviceInfo });

        FreeInputAudioDevice(audioDevicePtr);

        return device;
    }

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void GetActiveInputAudioDevices(out IntPtr devices, out int count);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void FreeInputAudioDevicesArray(IntPtr devices, int count);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr GetInputAudioDeviceInfo([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void FreeInputAudioDevice(IntPtr device);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void RegisterInputNotificationCallback(DeviceStateChangedCallback callback);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void UnregisterInputNotificationCallback();

    private delegate void DeviceStateChangedCallback([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);
}
