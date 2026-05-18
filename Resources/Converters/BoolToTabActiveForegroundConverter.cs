using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace LesserDashboardClient.Resources.Converters
{
    /// <summary>Aba ativa: texto Accent. Inativa: MetaFg (mesmo tom dos metadados do app).</summary>
    public class BoolToTabActiveForegroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var app = Application.Current;
            var variant = app?.ActualThemeVariant ?? ThemeVariant.Dark;

            if (value is true)
            {
                if (app?.TryGetResource("Accent", variant, out var accent) == true && accent is IBrush ab)
                    return ab;
                return new SolidColorBrush(Color.Parse("#FFFF9600"));
            }

            if (app?.TryGetResource("MetaFg", variant, out var meta) == true && meta is IBrush mb)
                return mb;
            if (app?.TryGetResource("TextFillColorSecondaryBrush", variant, out var sec) == true && sec is IBrush sb)
                return sb;
            return Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
