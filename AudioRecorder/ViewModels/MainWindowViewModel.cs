using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive;

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
    }
    private void ExecuteSaveCommand()
    {
        Debug.WriteLine("ExecuteSaveCommand!!!");
    }
    private void ExecuteStopCommand()
    {
        Debug.WriteLine("ExecuteStopCommand!!!");
    }
}
