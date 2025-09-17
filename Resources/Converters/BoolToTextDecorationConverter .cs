using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace LesserDashboardClient.Resources.Converters
{
    public class BoolToTextDecorationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                // Se true, retorna strikethrough
                return new TextDecorationCollection { new TextDecoration { Location = TextDecorationLocation.Strikethrough } };
            }

            // Se false, retorna vazio (sem decoração)
            return new TextDecorationCollection();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
