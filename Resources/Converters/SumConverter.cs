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
                // Verificar se values não é null e tem elementos
                if (values == null || values.Count == 0)
                    return "0";

                int sum = 0;

                foreach (var v in values)
                {
                    if (v is int i)
                        sum += i;
                    else if (v is int nullableInt)
                    {
                        sum += nullableInt;
                    }
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

