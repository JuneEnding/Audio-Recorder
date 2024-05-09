using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioRecorder.ViewModels;

public class MainProcess : ViewModelBase
{
    private string _name;
    private int _id;

    public string Name
    {
        get => _name;
        set
        {
            this.RaiseAndSetIfChanged(ref _name, value);
        }
    }

    public int ID
    {
        get => _id;
        set
        {
            this.RaiseAndSetIfChanged(ref _id, value);
        }
    }

    public MainProcess(string name, int id) : base()
    {
        Name = name;
        ID = id;
    }
}
