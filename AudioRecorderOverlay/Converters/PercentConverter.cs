using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AudioRecorderOverlay.Converters;

internal sealed class PercentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue && parameter is string strParameter && float.TryParse(strParameter, NumberStyles.Any, CultureInfo.InvariantCulture, out var percent))
        {
            return (int)(doubleValue * percent);
        }
        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}