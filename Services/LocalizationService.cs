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

        public void ApplyLanguage(string languageCode)
        {
            string targetLang = languageCode;

            // Lógica de fallback se vier vazio ou inválido
            if (string.IsNullOrEmpty(targetLang))
            {
                targetLang = LanguageHelper.GetComputerLanguage();
                CorruptionDiagnostics.Log($"ApplyLanguage (fallback): input vazio -> GetComputerLanguage='{targetLang}'");
            }

            if (string.IsNullOrEmpty(targetLang) || !LanguageHelper.SupportedLanguages.Contains(targetLang))
            {
                // Tenta pegar do sistema
                targetLang = CultureInfo.CurrentCulture.Name;

                // Se ainda não for suportado, fallback para en-US
                if (!LanguageHelper.SupportedLanguages.Contains(targetLang))
                {
                    targetLang = "en-US";
                    CorruptionDiagnostics.Log($"ApplyLanguage (fallback): culture não suportada -> en-US");
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
            // Fallback: Loc vazio (possível causa de "idioma trava em inglês")
            CorruptionDiagnostics.Log($"GetCurrentLanguage (fallback): Loc.Instance.CurrentLanguage vazio -> en-US");
            return "en-US";
        }
    }
}

















