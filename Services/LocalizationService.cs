using CodingSeb.Localization;
using LesserDashboardClient.Helpers;
using SharedClientSide.Helpers;
using System;
using System.Globalization;
using System.Linq;

namespace LesserDashboardClient.Services
{
    /// <summary>
    /// Serviço responsável apenas pela aplicação do idioma.
    /// Não gerencia persistência nem estado da aplicação.
    /// </summary>
    public class LocalizationService
    {
        private static LocalizationService _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event EventHandler LanguageChanged;

        private LocalizationService() { }

        /// <summary>Idioma padrão quando não há preferência salva ou sistema não é suportado (pt-BR).</summary>
        public const string DefaultLanguage = "pt-BR";

        public void ApplyLanguage(string languageCode)
        {
            string targetLang = languageCode;

            // Lógica de fallback se vier vazio ou inválido
            if (string.IsNullOrEmpty(targetLang))
            {
                targetLang = LanguageHelper.GetComputerLanguage();
                if (string.IsNullOrEmpty(targetLang))
                    targetLang = DefaultLanguage;
                CorruptionDiagnostics.Log($"ApplyLanguage (fallback): input vazio -> '{targetLang}'");
            }

            if (string.IsNullOrEmpty(targetLang) || !LanguageHelper.SupportedLanguages.Contains(targetLang))
            {
                targetLang = CultureInfo.CurrentCulture.Name;
                if (!LanguageHelper.SupportedLanguages.Contains(targetLang))
                {
                    targetLang = DefaultLanguage;
                    CorruptionDiagnostics.Log($"ApplyLanguage (fallback): culture não suportada -> '{DefaultLanguage}'");
                }
            }

            // Aplica nas bibliotecas de localização
            string oldLang = Loc.Instance.CurrentLanguage;
            Loc.Instance.CurrentLanguage = targetLang;
            LanguageHelper.SetLanguageInCurrentComputer(targetLang);

            // Dispara evento se mudou
            if (oldLang != targetLang)
            {
                LanguageChanged?.Invoke(this, EventArgs.Empty);

                // Limpa cache de combos (lógica específica do negócio que estava acoplada no App)
                ComboPriceService.ClearCache();
            }
        }

        public string GetCurrentLanguage()
        {
            string locLang = Loc.Instance.CurrentLanguage;
            if (!string.IsNullOrWhiteSpace(locLang))
                return locLang;
            CorruptionDiagnostics.Log($"GetCurrentLanguage (fallback): Loc vazio -> '{DefaultLanguage}'");
            return DefaultLanguage;
        }
    }
}

















