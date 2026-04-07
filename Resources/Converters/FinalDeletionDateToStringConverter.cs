using Avalonia.Data.Converters;
using Avalonia;
using System;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters;

public sealed class FinalDeletionDateToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        if (value is DateTimeOffset dto)
            return new DateTimeToStringConverter().Convert(dto, typeof(string), "deletion", culture) ?? string.Empty;

        if (DateTimeOffset.TryParse(value.ToString(), null, DateTimeStyles.RoundtripKind, out var parsed))
            return new DateTimeToStringConverter().Convert(parsed, typeof(string), "deletion", culture) ?? string.Empty;

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        AvaloniaProperty.UnsetValue;
}

