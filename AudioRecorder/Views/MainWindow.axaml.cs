using AudioRecorder.ViewModels;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using System.Linq;
using System;

namespace AudioRecorder.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
