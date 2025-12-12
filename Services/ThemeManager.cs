using Avalonia;
using Avalonia.Styling;
using System;

namespace LesserDashboardClient.Services
{
    /// <summary>
    /// Serviço responsável apenas pela aplicação visual do tema.
    /// Não gerencia persistência nem estado da aplicação.
    /// </summary>
    public class ThemeManager
    {
        private static ThemeManager _instance;
        public static ThemeManager Instance => _instance ??= new ThemeManager();

        private ThemeManager() { }

        public void ApplyTheme(string themeMode)
        {
            var app = Application.Current;
            if (app == null) return;

            switch (themeMode)
            {
                case "DarkMode":
                    app.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                case "LightMode":
                    app.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                default:
                    app.RequestedThemeVariant = ThemeVariant.Default;
                    break;
            }
        }

        public bool IsCurrentThemeDark()
        {
            var app = Application.Current;
            if (app == null) return false;

            if (app.ActualThemeVariant == ThemeVariant.Dark)
                return true;
                
            // Se for Default, precisamos checar qual é o efetivo, mas por simplificação:
            return app.RequestedThemeVariant == ThemeVariant.Dark; 
        }
    }
}
