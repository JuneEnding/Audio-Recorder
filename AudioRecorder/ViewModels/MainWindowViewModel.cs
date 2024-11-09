using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using AudioRecorder.Services;
using System.IO;
using AudioRecorder.Models;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Avalonia.Media.Imaging;

namespace AudioRecorder.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private AudioDataProcessor _processor = new();

    public ReactiveCommand<Unit, Unit> ExportCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> PlayCommand { get; }
    public ReactiveCommand<Unit, Unit> RecordCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    public MainWindowViewModel()
    {
        InitializeMainProcesses();
        InitializeAudioDevices();

        this.WhenAnyValue(vm => vm.ProcessSearchText)
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(_ => FilterProcesses());
        this.WhenAnyValue(vm => vm.AudioDevicesSearchText)
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(_ => FilterAudioDevices());

        ExportCommand = ReactiveCommand.Create(ExecuteExportCommand, Observable.Return(false));
        PauseCommand = ReactiveCommand.Create(ExecutePauseCommand, Observable.Return(false));
        PlayCommand = ReactiveCommand.Create(ExecutePlayCommand, Observable.Return(false));
        RecordCommand = ReactiveCommand.Create(ExecuteRecordCommand, this
            .WhenAnyValue(x => x.IsRecording)
            .Select(isRecording => !isRecording));
        SaveCommand = ReactiveCommand.Create(ExecuteSaveCommand, this
            .WhenAnyValue(x => x.IsRecording)
            .Select(isRecording => !isRecording));
        StopCommand = ReactiveCommand.Create(ExecuteStopCommand, this.WhenAnyValue(x => x.IsRecording));
    }

    private void ExecutePauseCommand()
    {
        Debug.WriteLine("ExecutePauseCommand!!!");
    }
    private void ExecuteExportCommand()
    {
        Debug.WriteLine("ExecuteExportCommand!!!");
    }
    private void ExecutePlayCommand()
    {
        Debug.WriteLine("ExecutePlayCommand!!!");
    }
    private void ExecuteRecordCommand()
    {
        Debug.WriteLine("ExecuteRecordCommand!!!");

        IsRecording = true;
        _activeRecordingProcesses = _mainProcesses.Where(p => p.IsChecked).Select(p => p.Id).ToList();
        _activeRecordingAudioDevices = _audioDevices.Where(ad => ad.IsChecked)
            .Select(ad => ad.ToNativeAudioDeviceInfo()).ToList();
        var captureId = AudioCapture.StartCapture(_activeRecordingProcesses, _activeRecordingAudioDevices);
        _processor.StartProcessing(_activeRecordingProcesses, _activeRecordingAudioDevices, captureId);
    }
    private void ExecuteSaveCommand()
    {
        Debug.WriteLine("ExecuteSaveCommand!!!");

        const string applicationName = "AudioRecorder";
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), applicationName);

        if (!Directory.Exists(basePath)) 
        { 
            Directory.CreateDirectory(basePath);
        }

        _processor.SaveLastAudioData(basePath);

        Debug.WriteLine($"Saved to {basePath}");
    }
    private void ExecuteStopCommand()
    {
        Debug.WriteLine("ExecuteStopCommand!!!");

        _processor.StopProcessing(_activeRecordingProcesses, _activeRecordingAudioDevices);
        AudioCapture.StopCapture(_activeRecordingProcesses, _activeRecordingAudioDevices);
        _activeRecordingProcesses.Clear();
        IsRecording = false;
    }

    private bool _isRecording = false;
    public bool IsRecording
    {
        get => _isRecording;
        set => this.RaiseAndSetIfChanged(ref _isRecording, value);
    }

    private List<MainProcess> _mainProcesses = new();
    private List<AudioDeviceInfo> _audioDevices = new();

    private ObservableCollection<MainProcess> _filteredProcesses = new();
    public ObservableCollection<MainProcess> FilteredProcesses
    {
        get => _filteredProcesses;
        set => this.RaiseAndSetIfChanged(ref _filteredProcesses, value);
    }

    private ObservableCollection<AudioDeviceInfo> _filteredAudioDevices = new();
    public ObservableCollection<AudioDeviceInfo> FilteredAudioDevices
    {
        get => _filteredAudioDevices;
        set => this.RaiseAndSetIfChanged(ref _filteredAudioDevices, value);
    }

    private string _processSearchText = "";
    public string ProcessSearchText
    {
        get => _processSearchText;
        set => this.RaiseAndSetIfChanged(ref _processSearchText, value);
    }

    private string _audioDevicesSearchText = "";
    public string AudioDevicesSearchText
    {
        get => _audioDevicesSearchText;
        set => this.RaiseAndSetIfChanged(ref _audioDevicesSearchText, value);
    }

    private List<uint> _activeRecordingProcesses = new();
    private List<AudioCapture.NativeAudioDeviceInfo> _activeRecordingAudioDevices = new();

    private void InitializeMainProcesses()
    {
        var audioController = new CoreAudioController();

        _mainProcesses = new List<MainProcess>();
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

                var process = new MainProcess(processName, (uint)processId, icon);
                _mainProcesses.Add(process);

                addedProcessIds.Add(processId);
            }
        }

        FilterProcesses();
    }

    private void FilterProcesses()
    {
        var filter = ProcessSearchText.ToLower();
        if (!string.IsNullOrEmpty(filter))
        {
            FilteredProcesses = new ObservableCollection<MainProcess>(_mainProcesses.Where(mainProcess =>
                mainProcess.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        else
        {
            FilteredProcesses = new ObservableCollection<MainProcess>(_mainProcesses);
        }
    }

    private void InitializeAudioDevices()
    {
        var audioDevices = AudioCapture.GetAudioDevicesArray();
        _audioDevices = audioDevices.Select(ad => new AudioDeviceInfo(ad)).ToList();
        FilterAudioDevices();
    }

    private void FilterAudioDevices()
    {
        var filter = AudioDevicesSearchText.ToLower();
        if (!string.IsNullOrEmpty(filter))
        {
            FilteredAudioDevices = new ObservableCollection<AudioDeviceInfo>
            (
                from audioDeviceInfo in _audioDevices
                where
                (
                    audioDeviceInfo.Name.ToLower().Contains(filter)
                )
                select audioDeviceInfo
            );
        }
        else
        {
            FilteredAudioDevices = new ObservableCollection<AudioDeviceInfo>(_audioDevices);
        }

    }
}
