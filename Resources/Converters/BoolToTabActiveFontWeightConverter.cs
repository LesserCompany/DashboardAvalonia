using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LesserDashboardClient.Resources.Converters
{
    /// <summary>Aba selecionada: texto em SemiBold para contraste com as inativas.</summary>
    public class BoolToTabActiveFontWeightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true ? FontWeight.SemiBold : FontWeight.Normal;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
