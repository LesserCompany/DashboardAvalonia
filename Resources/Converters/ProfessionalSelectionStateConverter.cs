using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LesserDashboardClient.Resources.Converters;

/// <summary>
/// Converte (username atual, username selecionado) em estado visual do card.
/// Retorna UnsetValue quando não selecionado para permitir estilos base + :pointerover.
/// </summary>
public class ProfessionalSelectionStateConverter : IMultiValueConverter
{
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#FF9600"));
    private static readonly IBrush SelectedBackground = new SolidColorBrush(Color.Parse("#14FF9600"));

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var currentUsername = values.Count > 0 ? values[0] as string : null;
        var selectedUsername = values.Count > 1 ? values[1] as string : null;
        var mode = parameter as string ?? string.Empty;
        var isSelected = !string.IsNullOrWhiteSpace(currentUsername)
            && !string.IsNullOrWhiteSpace(selectedUsername)
            && string.Equals(currentUsername, selectedUsername, StringComparison.OrdinalIgnoreCase);

        if (!isSelected)
            return AvaloniaProperty.UnsetValue;

        return mode switch
        {
            "BorderBrush" => SelectedBrush,
            "BorderThickness" => new Thickness(2),
            "Background" => SelectedBackground,
            "IsSelected" => true,
            _ => true
        };
    }
}
