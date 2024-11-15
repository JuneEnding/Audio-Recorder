using System;
using System.Globalization;
using AudioRecorderOverlay.Enums;
using Avalonia.Data.Converters;

namespace AudioRecorderOverlay.Converters;

internal sealed class RecordingStateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RecordingState recordingState)
        {
            return recordingState switch
            {
                RecordingState.Stopped => "Выключено",
                RecordingState.Recording => "Запись",
                RecordingState.Paused => "Пауза",
                RecordingState.Preparing => "Подготовка",
                RecordingState.Finalizing => "Завершение",
                _ => "Unknown State"
            };
        }
        return "Unknown State";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new NotImplementedException();
    }
}