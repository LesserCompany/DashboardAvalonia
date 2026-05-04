using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace LesserDashboardClient.Resources.Converters
{
    /// <summary>Converte bool (aba ativa) para Background: true = fundo suave de destaque, false = Transparent.</summary>
    public class BoolToTabActiveBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                var app = Application.Current;
                if (app?.TryGetResource("CardBgHover", app.ActualThemeVariant, out var resource) == true && resource is IBrush brush)
                    return brush;
                var isDark = app == null || app.ActualThemeVariant == ThemeVariant.Dark;
                return new SolidColorBrush(isDark ? Color.Parse("#FF404040") : Color.Parse("#FFE8E8E8"));
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
