using Avalonia.Media.Imaging;
using ReactiveUI;

namespace AudioRecorder.Core.Data;

public sealed class ProcessInfo : ReactiveObject
{

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private uint _id;
    public uint Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }

    private Bitmap? _icon;
    public Bitmap? Icon
    {
        get => _icon;
        set => this.RaiseAndSetIfChanged(ref _icon, value);
    }

    public ProcessInfo(string name, uint id, Bitmap? icon = null)
    {
        Name = string.IsNullOrEmpty(name) ? "Unknown" : name;
        Id = id;
        _icon = icon;
    }
}
