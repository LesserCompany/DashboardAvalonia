using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace LesserDashboardClient.Resources.Converters
{
    /// <summary>Aba ativa: borda sólida Accent (identidade #FF9600). Inativa: transparente.</summary>
    public class BoolToTabActiveBorderBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not true)
                return Brushes.Transparent;

            var app = Application.Current;
            var variant = app?.ActualThemeVariant ?? ThemeVariant.Dark;
            if (app?.TryGetResource("Accent", variant, out var r) == true && r is IBrush b)
                return b;
            return new SolidColorBrush(Color.Parse("#FFFF9600"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
