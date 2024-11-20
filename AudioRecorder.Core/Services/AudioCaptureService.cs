using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AudioRecorder.Core.Data;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;

namespace AudioRecorder.Core.Services;

public static class AudioCaptureService
{
    public static RangedObservableCollection<ProcessInfo> ProcessesInfo { get; } = new();

    public static long StartCapture(List<ProcessInfo> processes, List<NativeAudioDeviceInfo> audioDevices)
    {
        return StartCapture(processes.Select(process => process.Id).ToArray(), processes.Count, audioDevices.ToArray(),
            audioDevices.Count);
    }

    [DllImport("AudioCaptureLibrary.dll", CharSet = CharSet.Unicode)]
    public static extern void StopCapture(long captureId);

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
    private static extern IntPtr GetAudioDevices(out int deviceCount);

    [DllImport("AudioCaptureLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern void FreeAudioDevicesArray(IntPtr devices, int deviceCount);
}