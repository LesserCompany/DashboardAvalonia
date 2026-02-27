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

        public static OptionsModel Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonConvert.DeserializeObject<OptionsModel>(json);
                // Só loga quando carregou valores vazios/nulos (possível corrupção do arquivo)
                if (loaded != null && (string.IsNullOrWhiteSpace(loaded.Language) || string.IsNullOrWhiteSpace(loaded.AppTheme)))
                    CorruptionDiagnostics.Log($"OptionsModel.Load (suspeito) | Language='{loaded.Language ?? "(null)"}' | AppTheme='{loaded.AppTheme ?? "(null)"}'");
                return loaded;
            }

            var om = new OptionsModel
            {
                DefaultPathToDownloadProfessionalTaskFiles = SharedClientSide.Helpers.Constants.SeparationFolder.FullName
            };
            om.Save();
            return om;
        }

        public OptionsModel()
        {
        }
    }
}