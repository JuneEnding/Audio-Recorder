using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using AudioRecorder.Core.Data;
using AudioRecorder.Core.Services;
using DynamicData.Binding;
using ReactiveUI;

namespace AudioRecorderOverlay.ViewModels;

internal sealed class InputDevicesViewModel : ReactiveObject
{
    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    private readonly ObservableAsPropertyHelper<ObservableCollection<InputAudioDevice>> _filteredAudioDevices;
    public ObservableCollection<InputAudioDevice> FilteredAudioDevices => _filteredAudioDevices.Value;

    public InputDevicesViewModel()
    {
        var devicesChanged = InputAudioDeviceService.Instance.ActiveInputAudioDevices
            .ToObservableChangeSet()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(_ => Unit.Default);

        var filterChanged = this.WhenAnyValue(x => x.FilterText)
            .Throttle(TimeSpan.FromMicroseconds(300))
            .Select(_ => Unit.Default);

        devicesChanged
            .Merge(filterChanged)
            .Select(_ => FilterAudioDevices())
            .ToProperty(this, x => x.FilteredAudioDevices, out _filteredAudioDevices);
    }

    private ObservableCollection<InputAudioDevice> FilterAudioDevices()
    {
        var all = InputAudioDeviceService.Instance.ActiveInputAudioDevices;
        if (string.IsNullOrWhiteSpace(FilterText))
            return [.. all];

        var filtered = all
            .Where(d => d.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return [.. filtered];
    }
}