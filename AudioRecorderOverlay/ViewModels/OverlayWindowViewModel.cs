using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AudioRecorder.Core.Data;
using AudioRecorder.Core.Services;
using AudioRecorderOverlay.Enums;
using Avalonia.Threading;
using DynamicData.Binding;
using ReactiveUI;

namespace AudioRecorderOverlay.ViewModels
{
    public sealed class OverlayWindowViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> StartInstantReplayCommand { get; }
        public ReactiveCommand<Unit, Unit> StopInstantReplayCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveInstantReplayCommand { get; }
        public ReactiveCommand<Unit, Unit> StartCaptureCommand { get; }
        public ReactiveCommand<Unit, Unit> StopCaptureCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenLibraryFolderCommand { get; }

        private bool _isInstantReplayRunning;
        public bool IsInstantReplayRunning
        {
            get => _isInstantReplayRunning;
            private set => this.RaiseAndSetIfChanged(ref _isInstantReplayRunning, value);
        }

        private bool _isRecording;
        public bool IsRecording
        {
            get => _isRecording;
            private set => this.RaiseAndSetIfChanged(ref _isRecording, value);
        }

        private string _devicesFilterText = string.Empty;
        public string DevicesFilterText
        {
            get => _devicesFilterText;
            set => this.RaiseAndSetIfChanged(ref _devicesFilterText, value);
        }

        private RecordingState _instantReplayState;
        public RecordingState InstantReplayState
        {
            get => _instantReplayState;
            private set => this.RaiseAndSetIfChanged(ref _instantReplayState, value);
        }

        private RecordingState _recordingState;
        public RecordingState RecordingState
        {
            get => _recordingState;
            private set => this.RaiseAndSetIfChanged(ref _recordingState, value);
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

        private AudioDataProcessor? _activeInstantReplayProcessor;
        private AudioDataProcessor? _activeRecordingProcessor;

        public OverlayWindowViewModel()
        {
            _ = AudioCaptureService.InitializeAudioDevicesAsync();
            _ = AudioCaptureService.InitializeProcessesAsync();

            StartInstantReplayCommand = ReactiveCommand.CreateFromTask(StartInstantReplayAsync,
                this.WhenAnyValue(x => x.InstantReplayState).Select(state => state == RecordingState.Stopped));

            StopInstantReplayCommand = ReactiveCommand.CreateFromTask(StopInstantReplayAsync,
                this.WhenAnyValue(x => x.InstantReplayState).Select(state => state == RecordingState.Recording));

            SaveInstantReplayCommand = ReactiveCommand.CreateFromTask(SaveInstantReplayAsync,
                this.WhenAnyValue(x => x.InstantReplayState).Select(state => state == RecordingState.Recording));

            StartCaptureCommand = ReactiveCommand.CreateFromTask(StartCaptureAsync,
                this.WhenAnyValue(x => x.RecordingState).Select(state => state == RecordingState.Stopped));

            StopCaptureCommand = ReactiveCommand.CreateFromTask(StopCaptureAsync,
                this.WhenAnyValue(x => x.RecordingState).Select(state => state == RecordingState.Recording));

            OpenLibraryFolderCommand = ReactiveCommand.CreateFromTask(OpenLibraryFolderAsync);

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

        private async Task StartInstantReplayAsync()
        {
            InstantReplayState = RecordingState.Preparing;

            await Task.Run(() =>
            {
                var activeRecordingProcesses =
                    AudioCaptureService.ProcessesInfo.Where(p => p.IsChecked).ToList();
                var activeRecordingAudioDevices = AudioCaptureService.AudioDevicesInfo
                    .Where(ad => ad.IsChecked)
                    .Select(ad => ad.ToNativeAudioDeviceInfo()).ToList();

                if (activeRecordingProcesses.Count == 0 && activeRecordingAudioDevices.Count == 0)
                    Dispatcher.UIThread.Post(() => _ = StopInstantReplayAsync());

                var captureId = AudioCaptureService.StartCapture(activeRecordingProcesses, activeRecordingAudioDevices);
                _activeInstantReplayProcessor =
                    new AudioDataProcessor(captureId, activeRecordingProcesses, activeRecordingAudioDevices);
                _activeInstantReplayProcessor.Start();
            });

            InstantReplayState = RecordingState.Recording;
            IsInstantReplayRunning = true;
        }

        private async Task StopInstantReplayAsync()
        {
            InstantReplayState = RecordingState.Finalizing;

            if (_activeInstantReplayProcessor != null)
            {
                await Task.Run(() =>
                {
                    _activeInstantReplayProcessor.Stop();
                    AudioCaptureService.StopCapture(_activeInstantReplayProcessor.CaptureId);
                    _activeInstantReplayProcessor = null;
                });
            }

            InstantReplayState = RecordingState.Stopped;
            IsInstantReplayRunning = false;
        }

        private async Task SaveInstantReplayAsync()
        {
            await Task.Run(() =>
            {
                // TODO move to settings
                const string applicationName = "AudioRecorder";
                var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    applicationName);

                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                _activeInstantReplayProcessor?.SaveAllAudioData(basePath);
            });
        }

        private async Task StartCaptureAsync()
        {
            RecordingState = RecordingState.Preparing;

            await Task.Run(() =>
            {
                var activeRecordingProcesses =
                    AudioCaptureService.ProcessesInfo.Where(p => p.IsChecked).ToList();
                var activeRecordingAudioDevices = AudioCaptureService.AudioDevicesInfo
                    .Where(ad => ad.IsChecked)
                    .Select(ad => ad.ToNativeAudioDeviceInfo()).ToList();

                if (activeRecordingProcesses.Count == 0 && activeRecordingAudioDevices.Count == 0)
                    Dispatcher.UIThread.Post(() => _ = StopCaptureAsync());

                var captureId = AudioCaptureService.StartCapture(activeRecordingProcesses, activeRecordingAudioDevices);
                _activeRecordingProcessor =
                    new AudioDataProcessor(captureId, activeRecordingProcesses, activeRecordingAudioDevices);
                _activeRecordingProcessor.Start();
            });

            RecordingState = RecordingState.Recording;
            IsRecording = true;
        }

        private async Task StopCaptureAsync()
        {
            RecordingState = RecordingState.Finalizing;

            if (_activeRecordingProcessor != null)
            {
                await Task.Run(() =>
                {
                    _activeRecordingProcessor.Stop();
                    AudioCaptureService.StopCapture(_activeRecordingProcessor.CaptureId);

                    // TODO move to settings
                    const string applicationName = "AudioRecorder";
                    var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                        applicationName);

                    if (!Directory.Exists(basePath))
                        Directory.CreateDirectory(basePath);

                    Debug.WriteLine("Save audio");
                    _activeRecordingProcessor.SaveAllAudioData(basePath);
                    _activeRecordingProcessor = null;

                    Debug.WriteLine("Audio saved");
                });
            }

            RecordingState = RecordingState.Stopped;
            IsRecording = false;
        }

        private async Task OpenLibraryFolderAsync()
        {
            await Task.Run(() =>
            {
                // TODO move to settings
                const string applicationName = "AudioRecorder";
                var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    applicationName);

                if (Directory.Exists(basePath))
                    Process.Start("explorer.exe", basePath);

                // TODO else show error
            });
        }
    }
}
