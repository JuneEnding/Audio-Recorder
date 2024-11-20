using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AudioRecorder.Core.Data;
using System.Runtime.InteropServices;

namespace AudioRecorder.Core.Services;

public sealed class AudioDeviceService
{
    private const int DeviceStateActive = 0x00000001;
    private const int DeviceStateDisabled = 0x00000002;
    private const int DeviceStateUnplugged = 0x00000004;
    private const int DeviceStateNotPresent = 0x00000008;
    private const int DeviceStateMaskAll = 0x0000000F;

    [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
    private static DeviceStateChangedCallback? _deviceStateChangedCallback;

    private static readonly Lazy<AudioDeviceService> _instance = new(() => new AudioDeviceService());
    public static AudioDeviceService Instance => _instance.Value;

    public RangedObservableCollection<AudioDeviceInfo> ActiveAudioDevices { get; } = new();

    public event Action<string, int>? DeviceStateChanged;

    private AudioDeviceService()
    {
        try
        {
            _deviceStateChangedCallback = OnDeviceStateChanged;
            RegisterNotificationCallback(_deviceStateChangedCallback);
            var devices = GetActiveAudioDevices();
            ActiveAudioDevices.AddRange(devices);

            Logger.LogInfo("Listening for audio device changes...");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start listening for audio device changes: {ex}");
        }
    }

    ~AudioDeviceService()
    {
        UnregisterNotificationCallback();
    }

    private void OnDeviceStateChanged(string deviceId, int newState)
    {
        if (newState == DeviceStateActive)
        {
            var audioDeviceInfo = GetAudioDevice(deviceId);
            if (audioDeviceInfo == null)
                return;

            ActiveAudioDevices.Add(audioDeviceInfo);
        }
        else
        {
            var deviceToRemove = ActiveAudioDevices.First(device => device.Id == deviceId);
            ActiveAudioDevices.Remove(deviceToRemove);
        }

        DeviceStateChanged?.Invoke(deviceId, newState);
    }

    private static IEnumerable<AudioDeviceInfo> GetActiveAudioDevices()
    {
        GetActiveAudioDevices(out var devicesPtr, out var count);

        var devices = new AudioDeviceInfo[count];
        if (count == 0 || devicesPtr == IntPtr.Zero)
            return devices;

        var structSize = Marshal.SizeOf<NativeAudioDeviceInfo>();

        for (var i = 0; i < count; ++i)
        {
            var devicePtr = IntPtr.Add(devicesPtr, i * structSize);
            devices[i] = new AudioDeviceInfo(Marshal.PtrToStructure<NativeAudioDeviceInfo>(devicePtr));
        }

        FreeAudioDevicesArray(devicesPtr, count);

        return devices;
    }

    private static AudioDeviceInfo? GetAudioDevice(string deviceId)
    {
        var audioDevicePtr = GetAudioDeviceInfo(deviceId);

        if (audioDevicePtr == IntPtr.Zero)
            return null;

        var device = new AudioDeviceInfo(Marshal.PtrToStructure<NativeAudioDeviceInfo>(audioDevicePtr));

        FreeAudioDevice(audioDevicePtr);

        return device;
    }

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void GetActiveAudioDevices(out IntPtr devices, out int count);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void FreeAudioDevicesArray(IntPtr devices, int count);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr GetAudioDeviceInfo([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void FreeAudioDevice(IntPtr device);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void RegisterNotificationCallback(DeviceStateChangedCallback callback);

    [DllImport("AudioCaptureLibrary.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void UnregisterNotificationCallback();

    private delegate void DeviceStateChangedCallback([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);
}
