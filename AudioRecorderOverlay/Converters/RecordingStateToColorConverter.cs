using System;
using System.Globalization;
using AudioRecorderOverlay.Enums;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AudioRecorderOverlay.Converters;

internal class RecordingStateToColorConverter : IValueConverter
{
    private static IBrush? _defaultBrush;
    private static IBrush? _runningBrush;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RecordingState status && status == RecordingState.Recording)
        {
            if (_runningBrush == null)
            {
                object? brush = null;
                Application.Current?.TryFindResource("SystemFillColorSuccessBrush",
                    Application.Current.ActualThemeVariant, out brush);
                _runningBrush = brush as IBrush ?? Brushes.Green;
            }

            return _runningBrush;
        }

        if (_defaultBrush == null)
        {
            object? brush = null;
            Application.Current?.TryFindResource("TextFillColorPrimaryBrush", Application.Current.ActualThemeVariant, out brush);
            _defaultBrush = brush as IBrush ?? Brushes.Black;
        }

        return _defaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}