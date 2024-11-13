using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using AudioRecorder.Core.Data;
using AudioRecorder.Core.Services;
using DynamicData.Binding;
using ReactiveUI;

namespace AudioRecorderOverlay.ViewModels
{
    public sealed class OverlayWindowViewModel : ViewModelBase
    {
        private string _devicesFilterText = string.Empty;
        public string DevicesFilterText
        {
            get => _devicesFilterText;
            set => this.RaiseAndSetIfChanged(ref _devicesFilterText, value);
        }

        private string _processesFilterText = string.Empty;
        public string ProcessesFilterText
        {
            get => _processesFilterText;
            set => this.RaiseAndSetIfChanged(ref _processesFilterText, value);
        }

        private readonly ObservableAsPropertyHelper<ObservableCollection<AudioDeviceInfo>> _filteredAudioDevices;
        public ObservableCollection<AudioDeviceInfo> FilteredAudioDevices => _filteredAudioDevices.Value;

        private readonly ObservableAsPropertyHelper<ObservableCollection<ProcessInfo>> _filteredProcesses;
        public ObservableCollection<ProcessInfo> FilteredProcesses => _filteredProcesses.Value;

        public OverlayWindowViewModel()
        {
            _ = AudioCaptureService.InitializeAudioDevicesAsync();
            _ = AudioCaptureService.InitializeProcessesAsync();

            var audioDevicesChanged = AudioCaptureService.AudioDevicesInfo
                .ToObservableChangeSet()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(_ => Unit.Default);

            var processesChanged = AudioCaptureService.ProcessesInfo
                .ToObservableChangeSet()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(_ => Unit.Default);

            var devicesFilterChanged = this.WhenAnyValue(x => x.DevicesFilterText)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .Select(_ => Unit.Default);

            var processesFilterChanged = this.WhenAnyValue(x => x.ProcessesFilterText)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .Select(_ => Unit.Default);

            audioDevicesChanged.Merge(devicesFilterChanged)
                .Select(_ => FilterAudioDevices())
                .ToProperty(this, x => x.FilteredAudioDevices, out _filteredAudioDevices);

            processesChanged.Merge(processesFilterChanged)
                .Select(_ => FilterProcesses())
                .ToProperty(this, x => x.FilteredProcesses, out _filteredProcesses);
        }

        private ObservableCollection<AudioDeviceInfo> FilterAudioDevices()
        {
            var filtered = AudioCaptureService.AudioDevicesInfo
                .Where(device =>
                    string.IsNullOrWhiteSpace(DevicesFilterText) ||
                    device.Name.Contains(DevicesFilterText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new ObservableCollection<AudioDeviceInfo>(filtered);
        }

        private ObservableCollection<ProcessInfo> FilterProcesses()
        {
            var filtered = AudioCaptureService.ProcessesInfo
                .Where(process =>
                    string.IsNullOrWhiteSpace(ProcessesFilterText) ||
                    process.Name.Contains(ProcessesFilterText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new ObservableCollection<ProcessInfo>(filtered);
        }
    }
}
