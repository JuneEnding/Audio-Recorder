using Avalonia.Controls;

namespace AudioRecorderOverlay.Views;

internal sealed partial class OutputDevicesViewModel : UserControl
{
    public OutputDevicesViewModel()
    {
        InitializeComponent();
    }

    private void TreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        e.Handled = true;
    }
}