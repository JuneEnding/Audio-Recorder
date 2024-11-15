using System;
using System.Globalization;
using AudioRecorderOverlay.Enums;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AudioRecorderOverlay.Converters;

internal class RecordingStateFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RecordingState state)
        {
            return state == RecordingState.Recording ? FontWeight.Normal : FontWeight.Light;
        }

        return FontWeight.Light;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}