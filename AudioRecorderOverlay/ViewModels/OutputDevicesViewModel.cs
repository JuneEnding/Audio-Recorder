using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using AudioRecorder.Core.Data;
using AudioRecorder.Core.Services;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace AudioRecorderOverlay.ViewModels;

internal sealed class OutputDevicesViewModel : ReactiveObject
{
    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    private readonly ObservableAsPropertyHelper<ObservableCollection<OutputDeviceViewModel>> _filteredOutputDevices;
    public ObservableCollection<OutputDeviceViewModel> FilteredOutputDevices => _filteredOutputDevices.Value;

    public OutputDevicesViewModel()
    {
        var devicesChanged = OutputAudioDeviceService.Instance.ActiveOutputAudioDevices
            .ToObservableChangeSet()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default);

        var sessionsChanged = OutputAudioDeviceService.Instance.ActiveOutputAudioDevices
            .ToObservableChangeSet()
            .TransformMany(device => device.AudioSessions)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default);

        var filterChanged = this.WhenAnyValue(x => x.FilterText)
            .Throttle(TimeSpan.FromMicroseconds(300))
            .Select(_ => Unit.Default);

        sessionsChanged
            .Merge(devicesChanged)
            .Merge(filterChanged)
            .Select(_ => FilterAudioSessions())
            .ToProperty(this, x => x.FilteredOutputDevices, out _filteredOutputDevices);
    }

    private ObservableCollection<OutputDeviceViewModel> FilterAudioSessions()
    {
        var filtered = OutputAudioDeviceService.Instance.ActiveOutputAudioDevices
            .Select(device =>
            {
                var matchedSessions = new ObservableCollection<AudioSession>(device.AudioSessions
                    .Where(session => session.DisplayName.Contains(FilterText, StringComparison.OrdinalIgnoreCase)));

                var deviceMatches = device.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase);

                if (!deviceMatches && matchedSessions.Count == 0)
                    return null;

                var viewModel = new OutputDeviceViewModel(device)
                {
                    Sessions = matchedSessions
                };

                return viewModel;
            })
            .OfType<OutputDeviceViewModel>();

        return [.. filtered];
    }
}