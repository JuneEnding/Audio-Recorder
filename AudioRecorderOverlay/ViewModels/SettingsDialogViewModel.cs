using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AudioRecorder.Core.Services;
using Avalonia;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using ReactiveUI;

namespace AudioRecorderOverlay.ViewModels;

internal sealed class SettingsDialogViewModel : ReactiveObject
{
    private const string JsonFilePath = "settings.json";
    private const string SystemTheme = "System";
    private const string DarkTheme = "Dark";
    private const string LightTheme = "Light";

    private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    private readonly FluentAvaloniaTheme? _faTheme;

    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<SettingsDialogViewModel> _instance = new(() => new SettingsDialogViewModel());
    public static SettingsDialogViewModel Instance => _instance.Value;

    [JsonIgnore]
    public string[] AppThemes { get; } = { SystemTheme, LightTheme, DarkTheme };

    private string _currentAppTheme = SystemTheme;
    public string CurrentAppTheme
    {
        get => _currentAppTheme;
        set
        {
            if (_currentAppTheme == value)
                return;

            this.RaiseAndSetIfChanged(ref _currentAppTheme, value);

            var newTheme = GetThemeVariant(value);
            if (newTheme != null && Application.Current != null)
                Application.Current.RequestedThemeVariant = newTheme;

            if (_faTheme != null)
                _faTheme.PreferSystemTheme = value == SystemTheme;
        }
    }

    private int _instantReplayDurationSeconds = 120;
    public int InstantReplayDurationSeconds
    {
        get => _instantReplayDurationSeconds;
        set => this.RaiseAndSetIfChanged(ref _instantReplayDurationSeconds, value);
    }

    [JsonIgnore]
    public string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";

    public SettingsDialogViewModel()
    {
        _faTheme = Application.Current?.Styles[0] as FluentAvaloniaTheme;
    }

    private ThemeVariant? GetThemeVariant(string value)
    {
        switch (value)
        {
            case LightTheme:
                return ThemeVariant.Light;
            case DarkTheme:
                return ThemeVariant.Dark;
            case SystemTheme:
            default:
                return null;
        }
    }

    public void LoadSettings()
    {
        try
        {
            if (!File.Exists(JsonFilePath))
            {
                SaveSettings();
                return;
            }

            var json = File.ReadAllBytes(JsonFilePath);
            var settings = JsonSerializer.Deserialize<SettingsDialogViewModel>(json);
            if (settings == null)
                return;

            CurrentAppTheme = settings.CurrentAppTheme;
            InstantReplayDurationSeconds = settings.InstantReplayDurationSeconds;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load settings: {ex}");
        }
    }

    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, _jsonSerializerOptions);
            File.WriteAllText(JsonFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save settings: {ex}");
        }
    }
}