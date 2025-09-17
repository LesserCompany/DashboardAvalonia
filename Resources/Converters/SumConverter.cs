using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters
{
    public sealed class SumConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            try
            {
                int sum = 0;

                foreach (var v in values)
                {
                    if (v is int i)
                        sum += i;
                    else if (int.TryParse(v?.ToString(), out var parsed))
                        sum += parsed;
                }

                return sum.ToString();
            }
            catch
            {
                return "---";
            }
        }
    }
}

