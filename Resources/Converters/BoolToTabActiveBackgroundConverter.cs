using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace LesserDashboardClient.Resources.Converters
{
    /// <summary>Converte bool (aba ativa) para Background: true = sólido do tema (CollectionsTabActiveBg); false = Transparent.</summary>
    public class BoolToTabActiveBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not true)
                return Brushes.Transparent;

            var app = Application.Current;
            var variant = app?.ActualThemeVariant ?? ThemeVariant.Dark;
            if (app?.TryGetResource("CollectionsTabActiveBg", variant, out var r) == true && r is IBrush b)
                return b;
            if (app?.TryGetResource("CardBgHover", variant, out var fallback) == true && fallback is IBrush brush)
                return brush;
            return new SolidColorBrush(Color.Parse("#FF363636"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
