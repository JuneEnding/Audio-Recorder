using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AudioRecorderOverlay.Converters;

internal sealed class SecondToTimeStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int totalSeconds)
            return "0 ���.";

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
            return $"{hours} �." +
                   (minutes > 0 ? $" {minutes} ���." : "") +
                   (seconds > 0 ? $" {seconds} ���." : "");

        if (minutes > 0)
            return $"{minutes} ���." +
                   (seconds > 0 ? $" {seconds} ���." : "");

        return $"{seconds} ���.";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}