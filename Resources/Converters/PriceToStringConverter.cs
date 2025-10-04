using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LesserDashboardClient.Resources.Converters
{
    public class PriceToStringConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double price)
            {
                // Formatar como moeda brasileira com 4 casas decimais
                return price.ToString("0.0000", CultureInfo.GetCultureInfo("pt-BR"));
            }
            
            return "0.0000";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

