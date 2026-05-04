using Avalonia.Data.Converters;
using CodingSeb.Localization;
using System;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters;

/// <summary>Formata a mensagem de prazo para restaurar coleção a partir de <c>FinalScheduledDeletionDate</c>.</summary>
public sealed class RestorationDeadlineMessageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        DateTimeOffset dto;
        if (value is DateTimeOffset x)
            dto = x;
        else if (!DateTimeOffset.TryParse(value.ToString(), null, DateTimeStyles.RoundtripKind, out dto))
            return string.Empty;

        var dateStr = new DateTimeToStringConverter().Convert(dto, typeof(string), "deletion", culture)?.ToString() ?? "";
        if (string.IsNullOrEmpty(dateStr))
            return string.Empty;

        var template = Loc.Tr(
            "Your collection can be restored until {0}, after which it will be permanently removed.",
            "Sua coleção pode ser restaurada até {0}, depois disso ela será removida permanentemente.");
        return string.Format(culture, template, dateStr);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
