using System.Diagnostics.CodeAnalysis;
using AudioRecorder.Core.Data;
using System.Runtime.InteropServices;
using DynamicData;
using System.Collections.ObjectModel;
using DynamicData.Binding;

namespace AudioRecorder.Core.Services;

internal sealed class OutputAudioDeviceService
{
    private const int DeviceStateActive = 0x00000001;
    private const int DeviceStateDisabled = 0x00000002;
    private const int DeviceStateUnplugged = 0x00000004;
    private const int DeviceStateNotPresent = 0x00000008;
    private const int DeviceStateMaskAll = 0x0000000F;
    private const int AudioSessionStateInactive = 0;
    private const int AudioSessionStateActive = 1;
    private const int AudioSessionStateExpired = 2;

    [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
    private static DeviceStateChangedCallback? _deviceStateChangedCallback;

    private static SessionStateChangedCallback? _sessionStateChangedCallback;

    private static readonly Lazy<OutputAudioDeviceService> _instance = new(() => new OutputAudioDeviceService());
    public static OutputAudioDeviceService Instance => _instance.Value;

    public readonly RangedObservableCollection<OutputAudioDevice> ActiveOutputAudioDevices = new();

    private readonly ReadOnlyObservableCollection<AudioSession> _allAudioSessions;
    public ReadOnlyObservableCollection<AudioSession> AllAudioSessions => _allAudioSessions;

    public event Action<string, int>? DeviceStateChanged;

    private OutputAudioDeviceService()
    {
        ActiveOutputAudioDevices // TODO: проверить обновление UI при изменении устройств и сессий у устройств
            .ToObservableChangeSet()
            .AutoRefresh()
            .TransformMany(device => device.AudioSessions)
            .Bind(out _allAudioSessions)
            .Subscribe();

        try
        {
            _deviceStateChangedCallback = OnDeviceStateChanged;
            _sessionStateChangedCallback = OnSessionStateChanged;
            RegisterOutputNotificationCallback(_deviceStateChangedCallback);
            var audioDevices = GetOutputAudioDevices().ToArray();
            ActiveOutputAudioDevices.AddRange(audioDevices);

            foreach (var device in audioDevices)
            {
                RegisterSessionNotificationCallback(device.Id, _sessionStateChangedCallback);
            }

            Logger.LogInfo("Listening for audio device changes...");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start listening for audio device changes: {ex}");
        }
    }

    ~OutputAudioDeviceService()
    {
        UnregisterOutputNotificationCallback();
        foreach (var device in ActiveOutputAudioDevices)
        {
            UnregisterSessionNotificationCallback(device.Id);
        }
    }

    private void OnDeviceStateChanged(string deviceId, int newState)
    {
        if (newState == DeviceStateActive)
        {
            var audioDevice = GetOutputAudioDevice(deviceId);
            if (audioDevice == null)
                return;

            ActiveOutputAudioDevices.Add(audioDevice);
            if (_sessionStateChangedCallback != null)
                RegisterSessionNotificationCallback(deviceId, _sessionStateChangedCallback);
        }
        else
        {
            var deviceToRemove = ActiveOutputAudioDevices.FirstOrDefault(device => device.Id == deviceId);
            if (deviceToRemove == null)
                return;

            ActiveOutputAudioDevices.Remove(deviceToRemove);
            UnregisterSessionNotificationCallback(deviceId);
        }

        DeviceStateChanged?.Invoke(deviceId, newState);
    }

    private void OnSessionStateChanged(string deviceId, string sessionId, int newState)
    {
        var device = ActiveOutputAudioDevices.FirstOrDefault(d => d.Id == deviceId);
        if (device == null)
        {
            Logger.LogWarning($"Device {deviceId} not found in active devices");
            return;
        }

        var existing = device.AudioSessions.FirstOrDefault(s => s.SessionId == sessionId);

        if (newState == AudioSessionStateActive)
        {
            if (existing != null) return;
            Logger.LogInfo($"New session detected: {sessionId} for device {deviceId}");

            var updatedDevice = GetOutputAudioDevice(deviceId);
            var updatedSession = updatedDevice?.AudioSessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (updatedSession != null)
            {
                device.AudioSessions.Add(updatedSession);
            }
        }
        else
        {
            if (existing == null) return;
            Logger.LogInfo($"Session removed: {sessionId} from device {deviceId}");
            device.AudioSessions.Remove(existing);
        }
    }

    private static IEnumerable<OutputAudioDevice> GetOutputAudioDevices()
    {
        var audioDevices = new List<OutputAudioDevice>();

        GetActiveOutputAudioDevices(out var devicesPtr, out var count);

        if (devicesPtr == IntPtr.Zero || count == 0)
            return audioDevices;

        for (var i = 0; i < count; ++i)
        {
            var currentPtr = IntPtr.Add(devicesPtr, i * Marshal.SizeOf<OutputAudioDeviceInfo>());
            var deviceStruct = Marshal.PtrToStructure<OutputAudioDeviceInfo>(currentPtr);

            var audioDevice = new OutputAudioDevice(deviceStruct);
            audioDevices.Add(audioDevice);
        }

        FreeOutputAudioDevicesArray(devicesPtr, count);

        return audioDevices;
    }

    private static OutputAudioDevice? GetOutputAudioDevice(string deviceId)
    {
        var audioDevicePtr = GetOutputAudioDeviceInfo(deviceId);

        if (audioDevicePtr == IntPtr.Zero)
            return null;

        var deviceStruct = Marshal.PtrToStructure<OutputAudioDeviceInfo>(audioDevicePtr);
        var audioDevice = new OutputAudioDevice(deviceStruct);

        FreeOutputAudioDevice(audioDevicePtr);

        return audioDevice;
    }

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void GetActiveOutputAudioDevices(out IntPtr devices, out int count);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void FreeOutputAudioDevicesArray(IntPtr devices, int count);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr GetOutputAudioDeviceInfo([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void FreeOutputAudioDevice(IntPtr device);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void RegisterOutputNotificationCallback(DeviceStateChangedCallback callback);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void UnregisterOutputNotificationCallback();

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void RegisterSessionNotificationCallback([MarshalAs(UnmanagedType.LPWStr)] string deviceId, SessionStateChangedCallback callback);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void UnregisterSessionNotificationCallback([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    private delegate void DeviceStateChangedCallback([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);

    private delegate void SessionStateChangedCallback([MarshalAs(UnmanagedType.LPWStr)] string deviceId, [MarshalAs(UnmanagedType.LPWStr)] string sessionId, int newState);
}
