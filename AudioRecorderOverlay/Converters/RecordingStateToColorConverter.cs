using System;
using System.Globalization;
using AudioRecorderOverlay.Enums;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace AudioRecorderOverlay.Converters;

internal class RecordingStateToColorConverter : IValueConverter
{
    private static ThemeVariant? _previousThemeVariant;
    private static IBrush? _defaultBrush;
    private static IBrush? _runningBrush;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (_previousThemeVariant != Application.Current?.ActualThemeVariant)
        {
            _previousThemeVariant = Application.Current?.ActualThemeVariant;
            _defaultBrush = null;
            _runningBrush = null;
        }

        if (value is RecordingState status && status == RecordingState.Recording)
        {
            if (_runningBrush == null)
            {
                object? brush = null;
                Application.Current?.TryFindResource("SystemFillColorSuccessBrush", _previousThemeVariant, out brush);
                _runningBrush = brush as IBrush ?? Brushes.Green;
            }

            return _runningBrush;
        }

        if (_defaultBrush == null)
        {
            object? brush = null;
            Application.Current?.TryFindResource("TextFillColorPrimaryBrush", _previousThemeVariant, out brush);
            _defaultBrush = brush as IBrush ?? Brushes.Black;
        }

        return _defaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}