using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CodingSeb.Localization;
using CodingSeb.Localization.Loaders;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.Views;
using MsBox.Avalonia;
using Newtonsoft.Json;
using SharedClientSide.Helpers;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Login;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient;

public class App : Application
{
    public static AuthWindow? AuthWindowInstance { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        StartUpLanguageApp();
        StartUpThemeApp();
    }

    public override void OnFrameworkInitializationCompleted()
    {

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
             DisableAvaloniaDataAnnotationValidation();

            

            var lr = LesserFunctionClient.loginFileResult;

            if (lr == null || lr.loginFailed == true || lr.success == false || DateTime.Now > lr.User.loginTokenExpirationDate || GlobalAppStateViewModel.lfc.loginResult == null)
            {
                AuthWindowInstance = new AuthWindow();
                AuthWindowInstance.Show();
            }
            else
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
    public static void StartMainWindow()
    {
        if(Current != null)
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
                desktop.MainWindow.Show();
            }
    }
    public static void StartAuthWindow()
    {
        if (Current != null)
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                AuthWindowInstance = new AuthWindow();
                AuthWindowInstance.Show();
            }
    }
    private void StartUpLanguageApp()
    {
        LocalizationLoader.Instance.FileLanguageLoaders.Add(new JsonFileLoader());
        string basePath = AppContext.BaseDirectory;

        DirectoryInfo directory = new DirectoryInfo(Path.Combine(basePath, "Resources", "Translations"));
        foreach (FileInfo translationFile in directory.GetFiles("*.loc.json"))
        {
            string translationFilePath = Path.Combine(basePath, "Resources", "Translations", translationFile.Name);
            LocalizationLoader.Instance.AddFile(translationFilePath);
        }
        SetCurrentLang();
    }
    public void SetCurrentLang(string? language = "")
    {
        string currentLang;
        if (!string.IsNullOrEmpty(language))
            currentLang = language;
        else
            currentLang = LanguageHelper.GetComputerLanguage();

        if (string.IsNullOrEmpty(currentLang))
        {
            currentLang = System.Globalization.CultureInfo.CurrentCulture.Name;
            if(!LanguageHelper.SupportedLanguages.Contains(currentLang))
                currentLang = "en-US";
        }
        else
        {
            if (!LanguageHelper.SupportedLanguages.Contains(currentLang))
                currentLang = "en-US";
        }
            Loc.Instance.CurrentLanguage = currentLang;
        GlobalAppStateViewModel.Instance.AppLanguage = GetCurrentLang();
        LanguageHelper.SetLanguageInCurrentComputer(GetCurrentLang());
    }
    public string GetCurrentLang()
    {
        return !string.IsNullOrWhiteSpace(Loc.Instance.CurrentLanguage)
            ? Loc.Instance.CurrentLanguage
            : GetDefaultLanguage();
    }
    public string GetDefaultLanguage()
    {
        try
        {
            string currentCulture = System.Globalization.CultureInfo.CurrentCulture.Name;
            if (LanguageHelper.SupportedLanguages.Contains(currentCulture))
                return currentCulture;
            else
                return "en-US";
        }
        catch
        {
            return "en-US";
        }
    }
    private void StartUpThemeApp()
    {
        SwitchCurrentTheme(GlobalAppStateViewModel.options.AppTheme);
    }
    public void SwitchCurrentTheme(string theme = "")
    {
        var app = Application.Current;
        if (app == null)
            return;

        if (string.IsNullOrEmpty(theme))
        {
            app.RequestedThemeVariant =
                app.ActualThemeVariant == ThemeVariant.Dark
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
        }
        else
        {
            switch (theme)
            {
                case "DarkMode":
                    app.RequestedThemeVariant = ThemeVariant.Dark;
                    GlobalAppStateViewModel.Instance.AppIsDarkMode = true;
                    GlobalAppStateViewModel.options.AppTheme = "DarkMode";
                    break;
                case "LightMode":
                    app.RequestedThemeVariant = ThemeVariant.Light;
                    GlobalAppStateViewModel.Instance.AppIsDarkMode = false;
                    GlobalAppStateViewModel.options.AppTheme = "LightMode";

                    break;
                default:
                    app.RequestedThemeVariant = ThemeVariant.Default;
                    break;
            }
        }
        GlobalAppStateViewModel.options.Save();
    }

    public static async Task StartUploadConcurrentApp(ProfessionalTask professionalTask, Action<int> callback)
    {
        try
        {
            var json = JsonConvert.SerializeObject(professionalTask, Formatting.Indented);
            FileInfo fi = new FileInfo(AppInstaller.ClassToUploadTxtFilePath);
            if (fi == null || fi.Directory == null)
                return;
            if (!Directory.Exists(fi.Directory.FullName))
                Directory.CreateDirectory(fi.Directory.FullName);
            File.WriteAllText(AppInstaller.ClassToUploadTxtFilePath, json);


            AppInstaller ai = new AppInstaller("UploaderConcurrent", callback);
            await ai.startApp();
        }
        catch (Exception e)
        {
            if(MainWindow.instance != null)
            {
                var bbox = MessageBoxManager.GetMessageBoxStandard("", $"{e.Message} | {e.StackTrace}");
                var result = bbox.ShowWindowDialogAsync(MainWindow.instance);
            }
        }
    }
    public async static Task StartDownloadApp(ProfessionalTask professionalTask, Action<int> callback)
    {

        try
        {
            if (!Directory.Exists(AppInstaller.AppRootFolder))
                Directory.CreateDirectory(AppInstaller.AppRootFolder);
            File.WriteAllText(AppInstaller.AppRootFolder + "/classToDownload.txt", JsonConvert.SerializeObject(professionalTask, Formatting.Indented));

        }
        catch
        {
        }

        AppInstaller ai = new AppInstaller("download", callback);
        await ai.startApp("autostart");

    }
    public async static Task StartOrganizeApp(Action<int> callback)
    {
        AppInstaller ai = new AppInstaller("organize", callback);
        await ai.startApp();
    }




    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}