using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using AudioRecorder.Services;
using YourAppName.Services;
using System.IO;

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

        this.WhenAnyValue(vm => vm.ProcessSearchText)
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(_ => FilterProcesses());

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
        _activeRecordingProcesses = _mainProcesses.Where(p => p.IsChecked).Select(p => p.ID).ToList();
        var captureId = AudioCapture.StartCapture(_activeRecordingProcesses);
        _processor.StartProcessing(_activeRecordingProcesses, captureId);
    }
    private void ExecuteSaveCommand()
    {
        Debug.WriteLine("ExecuteSaveCommand!!!");

        string applicationName = "AudioRecorder";
        string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), applicationName);

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

        _processor.StopProcessing(_activeRecordingProcesses);
        AudioCapture.StopCapture(_activeRecordingProcesses);
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

    public ObservableCollection<MainProcess> _filteredProcesses = new();
    public ObservableCollection<MainProcess> FilteredProcesses
    {
        get => _filteredProcesses;
        set
        {
            this.RaiseAndSetIfChanged(ref _filteredProcesses, value);
        }
    }
    private string _processSearchText = "";
    public string ProcessSearchText
        {
        get => _processSearchText; 
        set
        {
            this.RaiseAndSetIfChanged(ref _processSearchText, value);
        }
    }

    private List<int> _activeRecordingProcesses = new();

    private void InitializeMainProcesses()
    {
        var windows = NativeMethods.GetAllWindows();
        _mainProcesses = windows.Select(w => new MainProcess(w.Title, w.ProcessId, w.Icon)).ToList();
        FilterProcesses();
    }

    private void FilterProcesses()
    {
        var filter = ProcessSearchText.ToLower();
        if (!string.IsNullOrEmpty(filter))
        {
            FilteredProcesses = new ObservableCollection<MainProcess> 
                (
                    from mainProcess in _mainProcesses
                    where
                    (
                        mainProcess.Name.ToLower().Contains(filter)
                    )
                    select mainProcess
                );
        }
        else
        {
            FilteredProcesses = new ObservableCollection<MainProcess>(_mainProcesses);
        }

    }
}
