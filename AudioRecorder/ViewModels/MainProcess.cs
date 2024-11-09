using Avalonia.Media.Imaging;
using ReactiveUI;

namespace AudioRecorder.ViewModels;

public class MainProcess : ViewModelBase
{
    private string _name = "";
    private uint _id;
    private bool _isChecked = false;
    private Bitmap? _icon;

    public string Name
    {
        get => _name;
        set
        {
            this.RaiseAndSetIfChanged(ref _name, value);
        }
    }

    public uint Id
    {
        get => _id;
        set
        {
            this.RaiseAndSetIfChanged(ref _id, value);
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            this.RaiseAndSetIfChanged(ref _isChecked, value);
        }
    }

    public Bitmap? Icon
    {
        get => _icon;
        set
        {
            this.RaiseAndSetIfChanged(ref _icon, value);
        }
    }

    public MainProcess(string name, uint id, Bitmap? icon = null) : base()
    {
        Name = string.IsNullOrEmpty(name) ? "Unknown" : name;
        Id = id;
        _icon = icon;
    }
}
