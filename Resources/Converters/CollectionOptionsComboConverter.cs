using Avalonia.Data.Converters;
using LesserDashboardClient.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace LesserDashboardClient.Resources.Converters
{
    public class CollectionOptionsComboConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 4)
                return null;

            return new CollectionComboOptions
            {
                BackupHd = values[0] is bool b1 && b1,
                AutoTreatment = values[1] is bool b2 && b2,
                Ocr = values[2] is bool b3 && b3,
                EnablePhotoSales = values[3] is bool b4 && b4
            };
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            if (value is CollectionComboOptions options)
            {
                return new object[]
                {
                options.BackupHd,
                options.AutoTreatment,
                options.Ocr,
                options.EnablePhotoSales
                };
            }

            return Array.Empty<object>();
        }
    }
}
