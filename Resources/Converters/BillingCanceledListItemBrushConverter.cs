using Avalonia.Data.Converters;
using Avalonia.Media;
using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters;

/// <summary>
/// Ajusta cores do badge/texto de cobrança cancelada na lista conforme o item está selecionado.
/// </summary>
public sealed class BillingCanceledListItemBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush SelectedForeground = new(Color.Parse("#FFFFFFFF"));
    private static readonly SolidColorBrush UnselectedForeground = new(Color.Parse("#FFE57373"));
    private static readonly SolidColorBrush SelectedBadgeBackground = new(Color.Parse("#66FFFFFF"));
    private static readonly SolidColorBrush UnselectedBadgeBackground = new(Color.Parse("#22DC2626"));
    private static readonly SolidColorBrush SelectedBadgeBorder = new(Color.Parse("#EEFFFFFF"));
    private static readonly SolidColorBrush UnselectedBadgeBorder = new(Color.Parse("#88DC2626"));

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = IsItemSelected(values);

        return (parameter as string)?.ToUpperInvariant() switch
        {
            "BADGEBACKGROUND" => isSelected ? SelectedBadgeBackground : UnselectedBadgeBackground,
            "BADGEBORDER" => isSelected ? SelectedBadgeBorder : UnselectedBadgeBorder,
            _ => isSelected ? SelectedForeground : UnselectedForeground
        };
    }

    private static bool IsItemSelected(IList<object?>? values)
    {
        if (values == null || values.Count < 2)
            return false;

        if (values[0] is not ProfessionalTask item || string.IsNullOrEmpty(item.classCode))
            return false;

        if (values[1] is not ProfessionalTask selected || string.IsNullOrEmpty(selected.classCode))
            return false;

        return string.Equals(item.classCode, selected.classCode, StringComparison.Ordinal);
    }
}
