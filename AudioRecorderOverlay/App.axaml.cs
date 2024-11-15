using System;
using AudioRecorder.Core.Services;
using AudioRecorderOverlay.ViewModels;
using AudioRecorderOverlay.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using ReactiveUI;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace AudioRecorderOverlay
{
    public sealed partial class App : Application
    {
        private TrayIcon? _trayIcon;
        private readonly Bitmap _applicationIcon;

        public App()
        {
            var iconUri = new Uri("avares://AudioRecorderOverlay/Assets/AudioRecorderIcon.png");
            var iconStream = AssetLoader.Open(iconUri);
            _applicationIcon = new Bitmap(iconStream);
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            if (Design.IsDesignMode)
                RequestedThemeVariant = ThemeVariant.Dark;
        }

        private void OnStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(_applicationIcon),
                ToolTipText = "Audio Recorder",
                Menu = new NativeMenu
                {
                    Items =
                    {
                        new NativeMenuItem
                        {
                            Header = "Открыть",
                            Gesture = new KeyGesture(Key.S, KeyModifiers.Alt | KeyModifiers.Shift),
                            Command = ReactiveCommand.Create(OpenOverlay)
                        },
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem
                        {
                            Header = "Выход",
                            Command = ReactiveCommand.Create(CloseApplication)
                        }
                    }
                }
            };
        }

        private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            _trayIcon?.Dispose();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            BindingPlugins.DataValidators.RemoveAt(0);

            SettingsDialogViewModel.Instance.LoadSettings();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Startup += OnStartup;
                desktop.Exit += OnExit;

                desktop.MainWindow = new OverlayWindow
                {
                    DataContext = new OverlayWindowViewModel()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void OpenOverlay()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var mainWindow = desktop.MainWindow;
            if (mainWindow == null)
                return;

            if (!mainWindow.IsVisible)
                mainWindow.Show();
        }

        private void CloseApplication()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        }
    }
}