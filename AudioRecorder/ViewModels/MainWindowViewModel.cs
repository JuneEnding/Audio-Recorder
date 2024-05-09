using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Management;
using AudioRecorder.ViewModels;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using AudioRecorder.Services;
using YourAppName.Services;

namespace AudioRecorder.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
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

        ExportCommand = ReactiveCommand.Create(ExecuteExportCommand);
        PauseCommand = ReactiveCommand.Create(ExecutePauseCommand);
        PlayCommand = ReactiveCommand.Create(ExecutePlayCommand);
        RecordCommand = ReactiveCommand.Create(ExecuteRecordCommand);
        SaveCommand = ReactiveCommand.Create(ExecuteSaveCommand);
        StopCommand = ReactiveCommand.Create(ExecuteStopCommand);

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

        _activeRecordingProcesses = _mainProcesses.Where(p => p.IsChecked).Select(p => p.ID).ToList();
        AudioCapture.StartCapture(_activeRecordingProcesses);
    }
    private void ExecuteSaveCommand()
    {
        Debug.WriteLine("ExecuteSaveCommand!!!");
    }
    private void ExecuteStopCommand()
    {
        Debug.WriteLine("ExecuteStopCommand!!!");

        AudioCapture.StopCapture(_activeRecordingProcesses);
        _activeRecordingProcesses.Clear();
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
