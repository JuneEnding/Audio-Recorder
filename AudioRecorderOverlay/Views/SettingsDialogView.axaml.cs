using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AudioRecorderOverlay.Views;

public sealed partial class SettingsDialogView : UserControl
{
    public SettingsDialogView()
    {
        InitializeComponent();

        LaunchRepoLinkItem.Click += LaunchRepoLinkItemClick;
    }

    private void LaunchRepoLinkItemClick(object sender, RoutedEventArgs e)
    {
        var uri = new Uri("https://github.com/JuneEnding/Audio-Recorder/issues");
        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString())
                { UseShellExecute = true, Verb = "open" });
        }
        catch
        {
            // ignored
        }
    }
}