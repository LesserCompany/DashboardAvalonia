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
        
        // Carrega as configurações primeiro
        GlobalAppStateViewModel.LoadOptionsModel();
        
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
                // Verifica o tipo de usuário para determinar qual janela mostrar
                if (lr.User.userType == "professionals")
                {
                    // Para separadores, mostra a ProfessionalWindow
                    desktop.MainWindow = new Views.ProfessionalWindow.ProfessionalWindowView(GlobalAppStateViewModel.lfc);
                }
                else
                {
                    // Para empresas, mostra a MainWindow normal
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = new MainWindowViewModel(),
                    };
                }
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
    public static void StartMainWindow()
    {
        if(Current != null)
            if (Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Reaplica as configurações de tema e idioma antes de criar a nova janela
                ReapplySettings();
                
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
                // Reaplica as configurações de tema e idioma antes de criar a nova janela
                ReapplySettings();
                
                AuthWindowInstance = new AuthWindow();
                AuthWindowInstance.Show();
            }
    }
    
    /// <summary>
    /// Reaplica as configurações de tema e idioma salvas
    /// </summary>
    public static void ReapplySettings()
    {
        // Recarrega as configurações do arquivo
        GlobalAppStateViewModel.LoadOptionsModel();
        
        // Reaplica o tema
        var app = Application.Current;
        if (app != null)
        {
            string savedTheme = GlobalAppStateViewModel.options.AppTheme;
            switch (savedTheme)
            {
                case "DarkMode":
                    app.RequestedThemeVariant = ThemeVariant.Dark;
                    GlobalAppStateViewModel.Instance.AppIsDarkMode = true;
                    break;
                case "LightMode":
                    app.RequestedThemeVariant = ThemeVariant.Light;
                    GlobalAppStateViewModel.Instance.AppIsDarkMode = false;
                    break;
                default:
                    app.RequestedThemeVariant = ThemeVariant.Default;
                    break;
            }
        }
        
        // Reaplica o idioma
        string savedLanguage = GlobalAppStateViewModel.options.Language;
        if (!string.IsNullOrEmpty(savedLanguage))
        {
            SetCurrentLang(savedLanguage);
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
        
        // Carrega o idioma salvo nas configurações
        string savedLanguage = GlobalAppStateViewModel.options.Language;
        if (!string.IsNullOrEmpty(savedLanguage))
        {
            SetCurrentLang(savedLanguage);
        }
        else
        {
            SetCurrentLang();
        }
    }
    public static void SetCurrentLang(string? language = "")
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
        
        // Salva o idioma nas configurações
        GlobalAppStateViewModel.options.Language = GetCurrentLang();
        GlobalAppStateViewModel.options.Save();
    }
    public static string GetCurrentLang()
    {
        return !string.IsNullOrWhiteSpace(Loc.Instance.CurrentLanguage)
            ? Loc.Instance.CurrentLanguage
            : GetDefaultLanguage();
    }
    public static string GetDefaultLanguage()
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

    public static async Task StartDiagramationWPFApp(Action<int> callback)
    {
        try
        {
            LesserFunctionClient.DefaultClient.RecordUserEvent("start_diagramation_app");
            AppInstaller ai = new AppInstaller("DiagramationWPF", callback);
            await ai.startApp();
        }
        catch (Exception e)
        {
            if (MainWindow.instance != null)
            {
                var bbox = MessageBoxManager.GetMessageBoxStandard("", $"{e.Message} | {e.StackTrace}");
                var result = bbox.ShowWindowDialogAsync(MainWindow.instance);
            }
        }
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