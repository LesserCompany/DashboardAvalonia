using Avalonia;
using Avalonia.Controls;
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

public partial class App : Application
{
    /// <summary>Product Id na Microsoft Store (quando o app é empacotado como MSIX). Substituir pelo id real.</summary>
    private const string MsixStoreProductId = "SEU_PRODUCT_ID";

    /// <summary>Se true, mostra a janela de "atualização disponível" (fase 1 - só avisar) no startup para demo. Colocar false em produção.</summary>
    private const bool ForceShowUpdateAvailableForDemo = false;
    /// <summary>Se true, mostra a janela de "atualização obrigatória" (fase 2 - bloquear) no startup para demo. Colocar false em produção.</summary>
    private const bool ForceShowUpdateRequiredForDemo = false;

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
        
        RegisterGlobalErrorHandlers();
        RegisterDispatcherDataGridExceptionHandler();
    }

    /// <summary>
    /// Trata exceção conhecida do DataGrid (GetPropertyIsReadOnly) ao clicar em células (ex.: CPF).
    /// Evita que o app feche; registra no log e mostra mensagem ao usuário.
    /// </summary>
    private void RegisterDispatcherDataGridExceptionHandler()
    {
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            var ex = e.Exception;
            string stack = ex?.StackTrace ?? "";

            // WebView.Avalonia (Windows): CoreWebView2.DOMContentLoaded pode ser entregue após o WebView2Core
            // já ter sido descartado (troca rápida de URL / fecho da vista). O pacote não protege o handler.
            if (ex is ObjectDisposedException ode &&
                (ode.ObjectName?.Contains("WebView2Core", StringComparison.OrdinalIgnoreCase) == true
                 || stack.Contains("WebView2Core", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    SaveLogError(ex.Message, stack, ex.InnerException?.ToString() ?? "Sem InnerException");
                }
                catch { /* não deixar falhar o handler */ }

                e.Handled = true;
                return;
            }

            if (ex != null && stack.Contains("GetPropertyIsReadOnly", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    SaveLogError(ex.Message, stack, ex.InnerException?.ToString() ?? "Sem InnerException");
                    e.Handled = true;
                    _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        try
                        {
                            var box = MessageBoxManager.GetMessageBoxStandard(
                                "Aviso",
                                "Ocorreu um erro ao editar a célula (por exemplo, CPF). Tente recarregar a coleção do servidor ou fechar e reabrir a tela. O aplicativo continuará em execução.",
                                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                                MsBox.Avalonia.Enums.Icon.Warning);
                            await box.ShowWindowDialogAsync(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
                        }
                        catch { /* evita segundo crash */ }
                    });
                }
                catch { /* não deixar falhar o handler */ }
            }
        };
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

            // Verificação de atualização MSIX no startup (só tem efeito quando o app está empacotado como MSIX)
            // Fase 2: atualização obrigatória — bloquear até o utilizador atualizar
            if (ForceShowUpdateRequiredForDemo)
            {
                MsixStoreUpdateChecker.CheckAndOpenStoreIfNeededAsync(MsixStoreProductId);
                ShowUpdateRequiredWindowAndShutdown(desktop);
                base.OnFrameworkInitializationCompleted();
                return;
            }
            MsixUpdateCheckResult? updateResult = null;
            try
            {
                updateResult = MsixStoreUpdateChecker.CheckAndOpenStoreIfNeededAsync(MsixStoreProductId).GetAwaiter().GetResult();
                if (updateResult == MsixUpdateCheckResult.Required)
                {
                    MsixStoreUpdateChecker.CheckAndOpenStoreIfNeededAsync(MsixStoreProductId);
                    ShowUpdateRequiredWindowAndShutdown(desktop);
                    base.OnFrameworkInitializationCompleted();
                    return;
                }
            }
            catch (Exception)
            {
                // App não está em MSIX ou verificação falhou: continuar normalmente
            }
            // Fase 1: marcar para mostrar depois do dashboard/login (para o aviso abrir por cima)
            if (ForceShowUpdateAvailableForDemo || updateResult == MsixUpdateCheckResult.Available)
                _showPhase1UpdateNoticeAfterWindowShown = true;

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
                SchedulePhase1UpdateNoticeAfterWindowShown();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // Inicializa lfc se token for válido
            var lfc = GlobalAppStateViewModel.lfc;
            if (lfc == null || lfc.loginResult == null || lfc.loginResult.User == null)
            {
                HandleInvalidToken(desktop);
                SchedulePhase1UpdateNoticeAfterWindowShown();
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
            SchedulePhase1UpdateNoticeAfterWindowShown();
        }
        
        base.OnFrameworkInitializationCompleted();
    }

    private static bool _showPhase1UpdateNoticeAfterWindowShown;

    /// <summary>Agenda o aviso da fase 1 para aparecer por cima do dashboard/login (após a janela estar visível).</summary>
    private static void SchedulePhase1UpdateNoticeAfterWindowShown()
    {
        if (!_showPhase1UpdateNoticeAfterWindowShown) return;
        _showPhase1UpdateNoticeAfterWindowShown = false;
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(500);
            MsixStoreUpdateChecker.CheckAndOpenStoreIfNeededAsync(MsixStoreProductId);
            ShowUpdateAvailableNotice();
        });
    }

    /// <summary>Fase 1 — Só avisar: janela de "atualização disponível". Não bloqueia; o utilizador pode fechar e continuar a usar a aplicação.</summary>
    private static void ShowUpdateAvailableNotice()
    {
        var message = "Há uma atualização disponível para esta aplicação. A Microsoft Store foi aberta. Pode continuar a usar a aplicação e atualizar quando quiser.";
        var window = new Window
        {
            Title = "Atualização disponível",
            Width = 450,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    }
                }
            }
        };
        ((Button)((StackPanel)window.Content).Children[1]).Click += (_, _) => window.Close();
        window.Show();
    }

    /// <summary>Fase 2 — Bloquear: janela de "atualização obrigatória". Nada funciona até o utilizador atualizar; ao fechar encerra a aplicação.</summary>
    private static void ShowUpdateRequiredWindowAndShutdown(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var message = "Existe uma atualização obrigatória para esta aplicação. A Microsoft Store foi aberta. Por favor, instale a atualização e volte a abrir a aplicação.";
        var window = new Window
        {
            Title = "Atualização obrigatória",
            Width = 450,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "Fechar",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    }
                }
            }
        };
        window.Closed += (_, _) => desktop.Shutdown(0);
        ((Button)((StackPanel)window.Content).Children[1]).Click += (_, _) => window.Close();
        desktop.MainWindow = window;
        window.Show();
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
        SharedClientSide.Helpers.AppInstaller.MsixLog("StartDownloadApp ENTRANDO");
        try
        {
            if (!Directory.Exists(AppInstaller.AppRootFolder))
                Directory.CreateDirectory(AppInstaller.AppRootFolder);
            File.WriteAllText(AppInstaller.AppRootFolder + "/classToDownload.txt", JsonConvert.SerializeObject(professionalTask, Formatting.Indented));
        }
        catch (Exception ex)
        {
            SharedClientSide.Helpers.AppInstaller.MsixLog($"StartDownloadApp exceção ao escrever classToDownload: {ex.Message}");
        }

        SharedClientSide.Helpers.AppInstaller.MsixLog("StartDownloadApp chamando InstallerRunner.RunInBackground(download)");
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

    private void RegisterGlobalErrorHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                if (e.ExceptionObject is Exception ex)
                {
                    SaveLogError(ex.Message, ex.StackTrace ?? "", ex.InnerException?.ToString() ?? "Sem InnerException");
                }
                else
                {
                    SaveLogError("Erro crítico desconhecido", "Sem StackTrace", e.ExceptionObject?.ToString() ?? "Sem detalhes");
                }
            }
            catch (Exception logEx)
            {
                Console.WriteLine("Falha ao registrar log: " + logEx.Message);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                SaveLogError(e.Exception.Message, e.Exception.StackTrace ?? "", e.Exception.InnerException?.ToString() ?? "Sem InnerException");
            }
            catch (Exception logEx)
            {
                Console.WriteLine("Falha ao registrar log: " + logEx.Message);
            }

            e.SetObserved(); // evita que o processo caia fora
        };
    }

    public static void SaveLogError(string message, string stacktrace, string innerException)
    {
        try
        {
            string assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "";
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string logDirectory = Path.Combine(documentsPath, "Separacao", "apps", assemblyName);
            string logFilePath = Path.Combine(logDirectory, "ERROR_LOG.txt");

            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            string logMessage = $"Data/Hora: {DateTime.Now}\n" +
                                $"Mensagem de erro: {message}\n" +
                                $"StackTrace: {stacktrace}\n" +
                                $"InnerException: {innerException}\n" +
                                $"-------------------------------------------\n";

            File.AppendAllText(logFilePath, logMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao salvar log: " + ex.Message);
        }
    }
}