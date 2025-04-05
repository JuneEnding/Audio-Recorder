using System.Collections.ObjectModel;
using AudioRecorder.Core.Data;

namespace AudioRecorderOverlay.ViewModels;

internal sealed class OutputDeviceViewModel
{
    public OutputAudioDevice Model { get; set; }
    public ObservableCollection<AudioSession> Sessions { get; set; }

    public OutputDeviceViewModel(OutputAudioDevice model)
    {
        Model = model;
        Sessions = model.AudioSessions;
    }

    public bool IsChecked
    {
        get => Model.IsChecked;
        set => Model.IsChecked = value;
    }

    public string Name => Model.Name;
}
