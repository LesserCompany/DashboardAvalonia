using CommunityToolkit.Mvvm.ComponentModel;
using LesserDashboardClient.Helpers;
using Newtonsoft.Json;
using SharedClientSide.Helpers;
using System;
using System.IO;

namespace LesserDashboardClient.Models.Company
{
    public partial class OptionsModel : ObservableObject
    {
        [ObservableProperty]
        private string defaultPathToDownloadProfessionalTaskFiles;

        [ObservableProperty]
        private string appTheme;

        [ObservableProperty]
        private string language;

        private static string AppConfigFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Separacao", "app");

        private static string SettingsFilePath =>
            Path.Combine(AppConfigFolder, "settings.json");

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this);
            if (!Directory.Exists(AppConfigFolder))
                Directory.CreateDirectory(AppConfigFolder);
            File.WriteAllText(SettingsFilePath, json);
            // Só loga em caso suspeito (ajuda a debugar corrupção)
            if (string.IsNullOrWhiteSpace(Language) || string.IsNullOrWhiteSpace(AppTheme))
                CorruptionDiagnostics.Log($"OptionsModel.Save (suspeito) | Language='{Language ?? "(null)"}' | AppTheme='{AppTheme ?? "(null)"}'");
        }

        /// <summary>Idioma padrão: português.</summary>
        public const string DefaultLanguage = "pt-BR";
        /// <summary>Tema padrão: escuro (maioria dos programas de foto usa preto).</summary>
        public const string DefaultAppTheme = "DarkMode";

        public static OptionsModel Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonConvert.DeserializeObject<OptionsModel>(json);
                if (loaded != null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.Language))
                    {
                        loaded.Language = DefaultLanguage;
                        CorruptionDiagnostics.Log($"OptionsModel.Load: Language vazio -> fallback '{DefaultLanguage}'");
                    }
                    if (string.IsNullOrWhiteSpace(loaded.AppTheme))
                    {
                        loaded.AppTheme = DefaultAppTheme;
                        CorruptionDiagnostics.Log($"OptionsModel.Load: AppTheme vazio -> fallback '{DefaultAppTheme}'");
                    }
                }
                return loaded ?? CreateDefault();
            }

            return CreateDefault();
        }

        private static OptionsModel CreateDefault()
        {
            var om = new OptionsModel
            {
                DefaultPathToDownloadProfessionalTaskFiles = SharedClientSide.Helpers.Constants.SeparationFolder.FullName,
                Language = DefaultLanguage,
                AppTheme = DefaultAppTheme
            };
            om.Save();
            return om;
        }

        public OptionsModel()
        {
        }
    }
}