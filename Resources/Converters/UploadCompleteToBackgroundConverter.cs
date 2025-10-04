using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LesserDashboardClient.Resources.Converters
{
    public class UploadCompleteToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool uploadComplete)
            {
                // Se o upload está completo (turma concluída), usa fundo chumbo
                // Se não está completo (turma ativa), usa fundo laranja
                return uploadComplete 
                    ? new SolidColorBrush(Color.Parse("#FF383735")) // Chumbo para concluídas
                    : new SolidColorBrush(Color.Parse("#FFFFA726")); // Laranja para ativas
            }
            
            // Valor padrão (chumbo)
            return new SolidColorBrush(Color.Parse("#FF383735"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
