using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AudioRecorder.Core.Data;
using AudioRecorder.Core.Services;
using AudioRecorderOverlay.Enums;
using AudioRecorderOverlay.Views;
using Avalonia;
using Avalonia.Threading;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace AudioRecorderOverlay.ViewModels;

internal sealed class OverlayWindowViewModel : ReactiveObject
{
    public ReactiveCommand<Unit, Unit> StartInstantReplayCommand { get; }
    public ReactiveCommand<Unit, Unit> StopInstantReplayCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveInstantReplayCommand { get; }
    public ReactiveCommand<Unit, Unit> StartCaptureCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCaptureCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenLibraryFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsDialogCommand { get; }

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

    private bool _isSettingsDialogOpened;
    public bool IsSettingsDialogOpened
    {
        get => _isSettingsDialogOpened;
        private set
        {
            if (_isSettingsDialogOpened == value)
                return;

            this.RaiseAndSetIfChanged(ref _isSettingsDialogOpened, value);

            if (!_isSettingsDialogOpened)
                _activeInstantReplayProcessor?.SetAllInstantReplayDuration(SettingsDialogViewModel.Instance
                    .InstantReplayDurationSeconds);
        }
    }

    private readonly ObservableAsPropertyHelper<ObservableCollection<InputAudioDevice>> _filteredAudioDevices;
    public ObservableCollection<InputAudioDevice> FilteredAudioDevices => _filteredAudioDevices.Value;

    private readonly ObservableAsPropertyHelper<ObservableCollection<AudioSession>> _filteredAudioSessions;
    public ObservableCollection<AudioSession> FilteredAudioSessions => _filteredAudioSessions.Value;
    
    private AudioDataProcessor? _activeInstantReplayProcessor;
    private AudioDataProcessor? _activeRecordingProcessor;

    public OverlayWindowViewModel()
    {
        if (Application.Current != null)
            Application.Current.PropertyChanged += OnApplicationPropertyChanged;
        
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

        OpenSettingsDialogCommand = ReactiveCommand.CreateFromTask(OpenSettingsDialogAsync);

        var audioDevicesChanged = InputAudioDeviceService.Instance.ActiveInputAudioDevices
            .ToObservableChangeSet()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default);

        var processesChanged = OutputAudioDeviceService.Instance.AllAudioSessions
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
            .ToProperty(this, x => x.FilteredAudioSessions, out _filteredAudioSessions);
    }

    ~OverlayWindowViewModel()
    {
        if (Application.Current != null)
            Application.Current.PropertyChanged -= OnApplicationPropertyChanged;
    }

    private void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Application.ActualThemeVariantProperty)
        {
            this.RaisePropertyChanged(nameof(InstantReplayState));
            this.RaisePropertyChanged(nameof(RecordingState));
        }
    }

    private ObservableCollection<InputAudioDevice> FilterAudioDevices()
    {
        var filtered = InputAudioDeviceService.Instance.ActiveInputAudioDevices
            .Where(device =>
                string.IsNullOrWhiteSpace(DevicesFilterText) ||
                device.Name.Contains(DevicesFilterText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ObservableCollection<InputAudioDevice>(filtered);
    }

    private ObservableCollection<AudioSession> FilterProcesses()
    {
        var filtered = OutputAudioDeviceService.Instance.AllAudioSessions
            .Where(process =>
                string.IsNullOrWhiteSpace(ProcessesFilterText) ||
                process.DisplayName.Contains(ProcessesFilterText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ObservableCollection<AudioSession>(filtered);
    }

    private async Task StartInstantReplayAsync()
    {
        InstantReplayState = RecordingState.Preparing;

        await Task.Run(() =>
        {
            var activeRecordingInputDevices = InputAudioDeviceService.Instance.ActiveInputAudioDevices
                .Where(d => d.IsChecked)
                .Select(d => d.ToAudioDeviceInfo()).ToArray();
            var activeRecordingOutputDevices = OutputAudioDeviceService.Instance.ActiveOutputAudioDevices
                .Where(s => s.IsChecked)
                .Select(s => s.ToAudioDeviceInfo()).ToArray();
            var activeRecordingAudioSessions = OutputAudioDeviceService.Instance.AllAudioSessions
                .Where(s => s.IsChecked)
                .Select(s => s.ToAudioSessionInfo()).ToArray();

            if (activeRecordingOutputDevices.Length == 0 && activeRecordingInputDevices.Length == 0 &&
                activeRecordingAudioSessions.Length == 0)
            {
                Dispatcher.UIThread.Post(() => _ = StopInstantReplayAsync());
                return;
            }

            var captureId = AudioCaptureService.StartCapture(activeRecordingInputDevices, activeRecordingOutputDevices, activeRecordingAudioSessions);
            _activeInstantReplayProcessor =
                new AudioDataProcessor(captureId, activeRecordingInputDevices, activeRecordingOutputDevices,
                    activeRecordingAudioSessions,
                    isInstantReplayMode: true,
                    instantReplayDuration: SettingsDialogViewModel.Instance.InstantReplayDurationSeconds);

            SettingsDialogViewModel.Instance
                .WhenAnyValue(vm => vm.InstantReplayDurationSeconds)
                .Subscribe(newDuration => _activeInstantReplayProcessor?.SetAllInstantReplayDuration(newDuration));

            var ok = _activeInstantReplayProcessor.Start();
            if (!ok)
                Dispatcher.UIThread.Post(() => _ = StopInstantReplayAsync());
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
            // TODO: move to settings
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
            var activeRecordingInputDevices = InputAudioDeviceService.Instance.ActiveInputAudioDevices
                .Where(d => d.IsChecked)
                .Select(d => d.ToAudioDeviceInfo()).ToArray();
            var activeRecordingOutputDevices = OutputAudioDeviceService.Instance.ActiveOutputAudioDevices
                .Where(s => s.IsChecked)
                .Select(s => s.ToAudioDeviceInfo()).ToArray();
            var activeRecordingAudioSessions = OutputAudioDeviceService.Instance.AllAudioSessions
                .Where(s => s.IsChecked)
                .Select(s => s.ToAudioSessionInfo()).ToArray();

            if (activeRecordingInputDevices.Length == 0 && activeRecordingOutputDevices.Length == 0 &&
                activeRecordingAudioSessions.Length == 0)
            {
                Dispatcher.UIThread.Post(() => _ = StopCaptureAsync());
                return;
            }

            var captureId = AudioCaptureService.StartCapture(activeRecordingInputDevices, activeRecordingOutputDevices, activeRecordingAudioSessions);
            _activeRecordingProcessor =
                new AudioDataProcessor(captureId, activeRecordingInputDevices, activeRecordingOutputDevices, activeRecordingAudioSessions);
            var ok = _activeRecordingProcessor.Start();
            if (!ok)
                Dispatcher.UIThread.Post(() => _ = StopCaptureAsync());
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

                _activeRecordingProcessor.SaveAllAudioData(basePath);
                _activeRecordingProcessor = null;
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

    private async Task OpenSettingsDialogAsync()
    {
        var originalTheme = SettingsDialogViewModel.Instance.CurrentAppTheme;
        var originalDuration = SettingsDialogViewModel.Instance.InstantReplayDurationSeconds;

        var contentDialog = new ContentDialog
        {
            Title = "Настройки",
            PrimaryButtonText = "Сохранить",
            CloseButtonText = "Назад",
            DefaultButton = ContentDialogButton.Primary,
            FullSizeDesired = true,
            Content = new SettingsDialogView
            {
                DataContext = SettingsDialogViewModel.Instance
            }
        };

        IsSettingsDialogOpened = true;
        var result = await contentDialog.ShowAsync();
        IsSettingsDialogOpened = false;

        if (result == ContentDialogResult.Primary)
        {
            SettingsDialogViewModel.Instance.SaveSettings();
        }
        else
        {
            SettingsDialogViewModel.Instance.CurrentAppTheme = originalTheme;
            SettingsDialogViewModel.Instance.InstantReplayDurationSeconds = originalDuration;
        }
    }
}
