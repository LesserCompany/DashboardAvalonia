using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace LesserDashboardClient.Resources.Converters
{
    public class UploadCompleteToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool uploadComplete)
            {
                // Tenta buscar os recursos dinâmicos do tema
                var app = Application.Current;
                if (app != null)
                {
                    // Se o upload está completo (turma concluída), usa fundo inativo
                    // Se não está completo (turma ativa), usa fundo ativo (laranja)
                    var resourceKey = uploadComplete 
                        ? "CardBackgroundInactiveBrush" 
                        : "CardBackgroundActiveBrush";
                    
                    if (app.TryGetResource(resourceKey, app.ActualThemeVariant, out var resource))
                    {
                        if (resource is SolidColorBrush brush)
                            return brush;
                    }
                }
                
                // Fallback: cores diretas baseadas no tema atual
                var isDarkMode = app?.ActualThemeVariant == ThemeVariant.Dark;
                
                if (uploadComplete)
                {
                    // Concluídas: cinza claro no light, chumbo no dark
                    return isDarkMode 
                        ? new SolidColorBrush(Color.Parse("#FF383735")) 
                        : new SolidColorBrush(Color.Parse("#FFF5F5F5"));
                }
                else
                {
                    // Ativas: laranja em ambos os temas
                    return new SolidColorBrush(Color.Parse("#FFFFA726"));
                }
            }
            
            // Valor padrão: usa o recurso inativo ou fallback
            var defaultApp = Application.Current;
            if (defaultApp != null && defaultApp.TryGetResource("CardBackgroundInactiveBrush", 
                defaultApp.ActualThemeVariant, out var defaultResource))
            {
                if (defaultResource is SolidColorBrush defaultBrush)
                    return defaultBrush;
            }
            
            var isDefaultDark = defaultApp?.ActualThemeVariant == ThemeVariant.Dark;
            return isDefaultDark 
                ? new SolidColorBrush(Color.Parse("#FF383735")) 
                : new SolidColorBrush(Color.Parse("#FFF5F5F5"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
