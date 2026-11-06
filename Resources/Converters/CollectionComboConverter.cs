using Avalonia.Data.Converters;
using Avalonia.Media;
using LesserDashboardClient.Models;
using LesserDashboardClient.Services;
using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters;

/// <summary>
/// Combo da coleção na lista: values[0] = a coleção da linha (ProfessionalTask), values[1] =
/// CollectionsViewModel.CombosForCollectionMatching. ConverterParameter: "Visible" (bool), "Title" (string) ou
/// "Color" (IBrush do combo). Mesmo esquema de MultiBinding do CollectionHasPartialAutoTreatmentConverter.
/// </summary>
public sealed class CollectionComboConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        CollectionComboOptions? combo = null;
        if (values != null && values.Count >= 2 && values[0] is ProfessionalTask pt)
            combo = CollectionComboMatcher.FindCombo(pt, values[1] as IEnumerable<CollectionComboOptions>);

        switch (parameter as string)
        {
            case "Title":
                return combo?.ComboTitle;
            case "Color":
                return ToBrush(combo?.ComboColorAccent);
            default:
                return combo != null && !string.IsNullOrWhiteSpace(combo.ComboTitle);
        }
    }

    internal static IBrush ToBrush(string? color)
    {
        if (!string.IsNullOrWhiteSpace(color) && Color.TryParse(color, out var c))
            return new SolidColorBrush(c);
        return Brushes.Gray;
    }
}
