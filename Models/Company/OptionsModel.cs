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
            AppSettingsLoader.MergeAndWriteSettings(this);
            if (string.IsNullOrWhiteSpace(Language) || string.IsNullOrWhiteSpace(AppTheme))
                CorruptionDiagnostics.Log($"OptionsModel.Save (suspeito) | Language='{Language ?? "(null)"}' | AppTheme='{AppTheme ?? "(null)"}'");
        }

        public const string DefaultLanguage = AppSettingsLoader.DefaultLanguage;
        public const string DefaultAppTheme = AppSettingsLoader.DefaultAppTheme;

        public static OptionsModel Load()
        {
            return AppSettingsLoader.LoadWithRepair(
                CreateDefault,
                RepairDefaults,
                model => model.Save(),
                reason => CorruptionDiagnostics.Log($"OptionsModel.Load: {reason} -> recriando defaults"));
        }

        private static bool RepairDefaults(OptionsModel model)
        {
            bool repaired = false;

            if (string.IsNullOrWhiteSpace(model.Language))
            {
                model.Language = DefaultLanguage;
                repaired = true;
                CorruptionDiagnostics.Log($"OptionsModel.Load: Language vazio -> fallback '{DefaultLanguage}'");
            }

            if (string.IsNullOrWhiteSpace(model.AppTheme))
            {
                model.AppTheme = DefaultAppTheme;
                repaired = true;
                CorruptionDiagnostics.Log($"OptionsModel.Load: AppTheme vazio -> fallback '{DefaultAppTheme}'");
            }

            return repaired;
        }

        private static OptionsModel CreateDefault()
        {
            var om = new OptionsModel
            {
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
