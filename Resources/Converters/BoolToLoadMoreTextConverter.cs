using Avalonia.Data.Converters;
using System;
using System.Globalization;
using CodingSeb.Localization;

namespace LesserDashboardClient.Resources.Converters;

public class BoolToLoadMoreTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLoading)
        {
            return isLoading ? Loc.Tr("Loading...") : Loc.Tr("Load old");
        }
        return Loc.Tr("Load old");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}