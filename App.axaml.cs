using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CodingSeb.Localization;
using CodingSeb.Localization.Loaders;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.Views;
using LesserDashboardClient.Helpers;
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
    public static AuthWindow? AuthWindowInstance { get; set; }
    private static bool isRedirecting = false; // Flag para evitar redirecionamentos duplos
    
    /// <summary>
    /// Evento disparado quando o idioma é alterado (mantido para compatibilidade se necessário, mas idealmente deve ser removido)
    /// </summary>
    public static event EventHandler? LanguageChanged;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Configura os carregadores de tradução (necessário antes de qualquer coisa que use Loc)
        InitializeLocalization();
        
        // Configura os delegates para override de endpoint usando o helper compartilhado
        SharedClientSide_AVALONIA.Helpers.EndpointConfigHelper.ConfigureLesserFunctionClientEndpoints();
        
        // Inicializa o estado global da aplicação (carrega settings e aplica tema/idioma)
        GlobalAppStateViewModel.Instance.InitializeApplicationState();
        
        // Assina o evento do serviço de localização para propagar (se necessário)
        LesserDashboardClient.Services.LocalizationService.Instance.LanguageChanged += (s, e) => LanguageChanged?.Invoke(s, e);
    }

    private void InitializeLocalization()
    {
        LocalizationLoader.Instance.FileLanguageLoaders.Add(new JsonFileLoader());
        string basePath = AppContext.BaseDirectory;

        DirectoryInfo directory = new DirectoryInfo(Path.Combine(basePath, "Resources", "Translations"));
        if (directory.Exists)
        {
            foreach (FileInfo translationFile in directory.GetFiles("*.loc.json"))
            {
                string translationFilePath = Path.Combine(basePath, "Resources", "Translations", translationFile.Name);
                LocalizationLoader.Instance.AddFile(translationFilePath);
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var lr = LesserFunctionClient.loginFileResult;
            
            // Lógica de validação do token simplificada e reutilizada
            bool isValidToken = false;
            if (lr != null && lr.User != null)
            {
                isValidToken = lr.loginFailed != true && lr.success && lr.User.loginTokenExpirationDate > DateTime.UtcNow;
            }
            
            if (!isValidToken)
            {
                HandleInvalidToken(desktop);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // Inicializa lfc se token for válido
            var lfc = GlobalAppStateViewModel.lfc;
            if (lfc == null || lfc.loginResult == null || lfc.loginResult.User == null)
            {
                HandleInvalidToken(desktop);
                base.OnFrameworkInitializationCompleted();
                return;
            }
            
            // Token válido, abre a janela correta
            if (lr.User.userType == "professionals")
            {
                desktop.MainWindow = new Views.ProfessionalWindow.ProfessionalWindowView(lfc);
            }
            else
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }
            
            // Verificação assíncrona pós-inicialização
            VerifyTokenAndValidateDirectory(desktop);
        }
        
        base.OnFrameworkInitializationCompleted();
    }

    private void HandleInvalidToken(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            if (LesserFunctionClient.loginFileInfo.Exists)
                LesserFunctionClient.loginFileInfo.Delete();
        }
        catch { }
        
        GlobalAppStateViewModel.ResetLesserFunctionClient();
        
        // NÃO reinicializar configurações - elas já estão aplicadas desde o início
        
        AuthWindowInstance = new AuthWindow();
        desktop.MainWindow = AuthWindowInstance;
    }
    
    private void VerifyTokenAndValidateDirectory(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            VerifyTokenImmediately(desktop);
        });
        
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(500);
            await GlobalAppStateViewModel.Instance.ValidateAndPromptDownloadDirectoryIfNeeded();
        });
    }
    
    private static void VerifyTokenImmediately(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var lr = LesserFunctionClient.loginFileResult;
            bool isStillValid = false;
            
            if (lr != null && lr.User != null)
            {
                isStillValid = lr.loginFailed != true && lr.success && lr.User.loginTokenExpirationDate > DateTime.UtcNow;
            }

            if (!isStillValid)
            {
                try
                {
                    if (LesserFunctionClient.loginFileInfo.Exists)
                        LesserFunctionClient.loginFileInfo.Delete();
                }
                catch { }
                
                GlobalAppStateViewModel.ResetLesserFunctionClient();
                RedirectToLoginWithMessage(desktop);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar token: {ex.Message}");
        }
    }
    
    private static void RedirectToLoginWithMessage(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var oldWindow = desktop.MainWindow;
            
            // NÃO reinicializar configurações - elas já estão aplicadas
            
            AuthWindowInstance = new AuthWindow();
            desktop.MainWindow = AuthWindowInstance;
            AuthWindowInstance.Show();
            
            _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    var box = MessageBoxManager.GetMessageBoxStandard(
                        Loc.Tr("Session expired"),
                        Loc.Tr("Your session has expired or is invalid. Please login again."),
                        MsBox.Avalonia.Enums.ButtonEnum.Ok
                    );
                    
                    await box.ShowWindowDialogAsync(AuthWindowInstance);
                }
                catch { }
            });
            
            if (oldWindow != null)
            {
                (oldWindow.DataContext as IDisposable)?.Dispose();
                oldWindow.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao redirecionar: {ex.Message}");
        }
    }
    
    public static void RedirectToLoginScreen()
    {
        try
        {
            if (isRedirecting) return;
            
            isRedirecting = true;
            
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var oldWindow = desktop.MainWindow;
                if (oldWindow is AuthWindow)
                {
                    isRedirecting = false;
                    return;
                }
                
                // NÃO reinicializar configurações - elas já estão aplicadas
                
                AuthWindowInstance = new AuthWindow();
                desktop.MainWindow = AuthWindowInstance;
                AuthWindowInstance.Show();
                
                if (oldWindow != null)
                {
                    (oldWindow.DataContext as IDisposable)?.Dispose();
                    oldWindow.Close();
                }
            }
            
            _ = Task.Delay(2000).ContinueWith(_ => { isRedirecting = false; });
        }
        catch
        {
            isRedirecting = false;
        }
    }
    
    public static void StartMainWindow()
    {
        if(Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // NÃO reinicializar configurações - elas já estão aplicadas
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            desktop.MainWindow.Show();
        }
    }
    
    public static void StartAuthWindow()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // NÃO reinicializar configurações - elas já estão aplicadas
            
            AuthWindowInstance = new AuthWindow();
            AuthWindowInstance.Show();
        }
    }

    public static void StartUploadConcurrentApp(ProfessionalTask professionalTask, Action<int> callback)
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

            // Usar InstallerRunner para executar em background e evitar travada da UI
            InstallerRunner.RunInBackground(
                appName: "UploaderConcurrent",
                onUiProgress: callback,
                args: "",
                onUiDone: () => callback?.Invoke(100),
                onUiError: msg => 
                {
                    if(MainWindow.instance != null)
                    {
                        var bbox = MessageBoxManager.GetMessageBoxStandard("", $"Erro na instalação: {msg}");
                        var result = bbox.ShowWindowDialogAsync(MainWindow.instance);
                    }
                }
            );
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

    public static void StartDownloadApp(ProfessionalTask professionalTask, Action<int> callback)
    {
        StartDownloadApp(professionalTask, callback, null, null);
    }

    public static void StartDownloadApp(ProfessionalTask professionalTask, Action<int> progressCallback, Action onDone, Action<string> onError)
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

        // Usar InstallerRunner para executar em background e evitar travada da UI
        InstallerRunner.RunInBackground(
            appName: "download",
            onUiProgress: progressCallback,
            args: "autostart",
            onUiDone: onDone,
            onUiError: onError
        );
    }

    public static void StartOrganizeApp(Action<int> callback)
    {
        // Usar InstallerRunner para executar em background e evitar travada da UI
        InstallerRunner.RunInBackground(
            appName: "organize",
            onUiProgress: callback,
            args: "",
            onUiDone: () => callback?.Invoke(100),
            onUiError: msg => 
            {
                if(MainWindow.instance != null)
                {
                    var bbox = MessageBoxManager.GetMessageBoxStandard("", $"Erro na instalação: {msg}");
                    var result = bbox.ShowWindowDialogAsync(MainWindow.instance);
                }
            }
        );
    }

    public static void StartDiagramationWPFApp(Action<int> callback)
    {
        try
        {
            LesserFunctionClient.DefaultClient.RecordUserEvent("start_diagramation_app");
            // Usar InstallerRunner para executar em background e evitar travada da UI
            InstallerRunner.RunInBackground(
                appName: "DiagramationWPF",
                onUiProgress: callback,
                args: "",
                onUiDone: () => callback?.Invoke(100),
                onUiError: msg => 
                {
                    if(MainWindow.instance != null)
                    {
                        var bbox = MessageBoxManager.GetMessageBoxStandard("", $"Erro na instalação: {msg}");
                        var result = bbox.ShowWindowDialogAsync(MainWindow.instance);
                    }
                }
            );
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
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}