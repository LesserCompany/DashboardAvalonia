using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LesserDashboardClient.Resources.Converters
{
    /// <summary>Visível só quando há desconto percentual &gt; 0 (oculta 0% e null).</summary>
    public class DiscountPercentVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            return value switch
            {
                double d => Math.Abs(d) > 0.0001,
                float f => Math.Abs(f) > 0.0001f,
                decimal m => m != 0,
                int i => i != 0,
                long l => l != 0,
                _ => false
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
