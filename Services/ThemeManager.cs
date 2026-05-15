using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
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

        /// <summary>Disparado no UI thread após o tema ser aplicado (útil para invalidar bindings que leem ThemeDictionaries em conversores).</summary>
        public static event EventHandler? ThemeApplied;

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

            // ActualThemeVariant e ThemeDictionaries consolidam no próximo passo de layout
            Dispatcher.UIThread.Post(
                static () => ThemeApplied?.Invoke(null, EventArgs.Empty),
                DispatcherPriority.Loaded);
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
