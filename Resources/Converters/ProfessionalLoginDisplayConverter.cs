using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using LesserDashboardClient.ViewModels.Collections;

namespace LesserDashboardClient.Resources.Converters;

/// <summary>
/// Converte professionalLogin (username) para nome amigável (surname) quando disponível.
/// </summary>
public class ProfessionalLoginDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var login = value as string;
        if (string.IsNullOrWhiteSpace(login))
            return login;

        var professionals = CollectionsViewModel.Instance?.Professionals;
        var professional = professionals?.FirstOrDefault(p =>
            string.Equals(p.username, login, StringComparison.OrdinalIgnoreCase));

        if (professional != null && !string.IsNullOrWhiteSpace(professional.surname))
            return professional.surname.Trim();

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
