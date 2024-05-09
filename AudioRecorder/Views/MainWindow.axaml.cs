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

    private void ProcessCheckBox_Checked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        CheckBox checkBox = (CheckBox)sender;
        MainProcess process = (MainProcess)checkBox.DataContext;
        string processName = process.Name;

        if (ViewModel.LinkedProcesses.Any(linkedProc => linkedProc.ID == process.ID && linkedProc.Name == process.Name))
        {
            ViewModel.LinkedProcesses.Remove(process);
        }
        else
        {
            ViewModel.LinkedProcesses.Add(process);
        }
    }

}
