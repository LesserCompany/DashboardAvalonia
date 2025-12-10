using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters
{
    public class DateTimeToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (DateTimeOffset.TryParse(value?.ToString(), null, DateTimeStyles.RoundtripKind, out DateTimeOffset dateTimeOffset))
            {
                var localTimeZone = TimeZoneInfo.Local;
                DateTimeOffset adjustedTime = dateTimeOffset.AddHours(localTimeZone.BaseUtcOffset.TotalHours);

                // Se o parâmetro for "withAt" ou "deletion", adiciona "às" antes do horário
                if (parameter?.ToString() == "withAt" || parameter?.ToString() == "deletion")
                {
                    // Formata a data e hora e adiciona "às" antes do horário
                    string datePart = adjustedTime.ToString("D", CultureInfo.CurrentCulture); // Data completa
                    string timePart = adjustedTime.ToString("HH:mm", CultureInfo.CurrentCulture); // Hora
                    return $"{datePart} às {timePart}";
                }

                // Formata a data e hora padrão
                string formattedDateTime = adjustedTime.ToString("f", CultureInfo.CurrentCulture);

                return formattedDateTime;

            }
            else
                return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Não necessário para exibição (one-way). Retorne null ou implemente parse se precisar.
            return null;
        }
    }
}
