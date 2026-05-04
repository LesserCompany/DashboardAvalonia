using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters;

/// <summary>Visível quando a aba Deletadas está ativa e o item tem data final de exclusão agendada.</summary>
public sealed class DeletedTabAndDateToVisibleConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count < 2)
            return false;

        if (values[0] is not bool tabDeleted || !tabDeleted)
            return false;

        var v = values[1];
        if (v == null || v == AvaloniaProperty.UnsetValue)
            return false;

        return v switch
        {
            DateTimeOffset => true,
            _ => DateTimeOffset.TryParse(v.ToString(), null, DateTimeStyles.RoundtripKind, out _)
        };
    }
}
