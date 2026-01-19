using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LesserDashboardClient.Resources.Converters
{
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue;
            
            if (value != null && value.GetType() == typeof(bool?))
            {
                var nullableBool = (bool?)value;
                return nullableBool ?? false;
            }
            
            // Para outros tipos nullable (DateTimeOffset?, int?, etc.), retorna true se não for null
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue;
            
            return false;
        }
    }
}
