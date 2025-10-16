using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters;

public class BoolToLoadMoreTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLoading)
        {
            return isLoading ? "Carregando..." : "Carregar antigas";
        }
        return "Carregar antigas";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}






