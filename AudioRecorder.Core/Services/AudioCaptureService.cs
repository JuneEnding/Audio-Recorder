using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AudioRecorder.Core.Data;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;

namespace AudioRecorder.Core.Services;

public static class AudioCaptureService
{
    public static RangedObservableCollection<AudioDeviceInfo> AudioDevicesInfo { get; } = new();
    public static RangedObservableCollection<ProcessInfo> ProcessesInfo { get; } = new();

    public static long StartCapture(List<uint> processIds, List<NativeAudioDeviceInfo> audioDevices)
    {
        foreach (var device in audioDevices)
            Debug.WriteLine($"GetAudioDevices: ${device.PipeId}, {device.Id}, {device.Name}");

        return StartCapture(processIds.ToArray(), processIds.Count, audioDevices.ToArray(), audioDevices.Count);
    }

    public static void StopCapture(List<uint> processIds, List<NativeAudioDeviceInfo> audioDevices)
    {
        StopCapture(processIds.ToArray(), processIds.Count, audioDevices.ToArray(), audioDevices.Count);
    }

    public static async Task InitializeAudioDevicesAsync()
    {
        var result = await Task.Run(() =>
        {
            var deviceArrayPtr = GetAudioDevices(out var deviceCount);
            if (deviceArrayPtr == IntPtr.Zero || deviceCount == 0)
                return null;

            var devices = new NativeAudioDeviceInfo[deviceCount];
            var currentPtr = deviceArrayPtr;

            var structSize = Marshal.SizeOf(typeof(NativeAudioDeviceInfo));
            for (var i = 0; i < deviceCount; ++i)
            {
                devices[i] = Marshal.PtrToStructure<NativeAudioDeviceInfo>(currentPtr);
                currentPtr = IntPtr.Add(currentPtr, structSize);
            }

            FreeAudioDevicesArray(deviceArrayPtr, deviceCount);

            return devices.Select(device => new AudioDeviceInfo(device));
        }).ConfigureAwait(false);

        if (result == null)
            AudioDevicesInfo.Clear();
        else
            AudioDevicesInfo.Refresh(result.ToList());
    }

    public static async Task InitializeProcessesAsync()
    {
        var result = await Task.Run(() =>
        {
            var audioController = new CoreAudioController();

            var processes = new ObservableCollection<ProcessInfo>();
            var addedProcessIds = new HashSet<int>();
            foreach (var playbackDevice in audioController.GetPlaybackDevices().Where(pbd => pbd.State == DeviceState.Active))
            {
                foreach (var session in playbackDevice.SessionController.All())
                {
                    var processId = session.ProcessId;
                    if (addedProcessIds.Contains(processId)) continue;

                    if (Process.GetProcessesByName("explorer").FirstOrDefault()?.Id == processId)
                        continue;

                    var icon = ResourceHelper.GetBitmapFromIconPath(session.IconPath);
                    icon ??= NativeMethods.GetIconForProcess(processId);

                    var processName = session.IsSystemSession ? ResourceHelper.GetLocalizedStringFromResource(session.DisplayName) : session.DisplayName;

                    var process = new ProcessInfo(processName, (uint)processId, icon);
                    processes.Add(process);

                    addedProcessIds.Add(processId);
                }
            }

            return processes;
        }).ConfigureAwait(false);

        ProcessesInfo.Refresh(result);
    }

    [DllImport("AudioCaptureLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern long StartCapture([In] uint[] processIds, int pidCount, [In] NativeAudioDeviceInfo[] audioDevices, int deviceCount);

    [DllImport("AudioCaptureLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern void StopCapture([In] uint[] processIds, int pidCount, [In] NativeAudioDeviceInfo[] audioDevices, int deviceCount);

    [DllImport("AudioCaptureLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetAudioDevices(out int deviceCount);

    [DllImport("AudioCaptureLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern void FreeAudioDevicesArray(IntPtr devices, int deviceCount);
}