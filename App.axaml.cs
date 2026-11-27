using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using CodingSeb.Localization;
using CodingSeb.Localization.Loaders;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.ViewModels.Update;
using LesserDashboardClient.Services.Store;
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

            // Abrir UI primeiro — nunca bloquear a thread de UI com GetResult() no check de update.
            // A checagem (Store / instalador web antigo) corre async depois da janela.
            OpenStartupWindow(desktop);
            ScheduleStartupUpdateCheck(desktop);
        }
        
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Cria Auth/Main conforme token. Não depende do check de update.
    /// </summary>
    private void OpenStartupWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var lr = LesserFunctionClient.loginFileResult;

        bool isValidToken = false;
        if (lr != null && lr.User != null)
        {
            // Além da data guardada, exige que o loginToken seja um JWT legível com exp no futuro —
            // token opaco/legado ou malformado cai direto no fluxo de login (HandleInvalidToken).
            isValidToken = lr.loginFailed != true && lr.success
                && lr.User.loginTokenExpirationDate > DateTime.UtcNow
                && JwtSessionInfo.IsValidJwt(lr.User.loginToken);
        }

        if (!isValidToken)
        {
            HandleInvalidToken(desktop);
            return;
        }

        var lfc = GlobalAppStateViewModel.lfc;
        if (lfc == null || lfc.loginResult == null || lfc.loginResult.User == null)
        {
            HandleInvalidToken(desktop);
            return;
        }

        // Company e professional usam o mesmo dashboard; o professional vê conforme as permissões
        // dele (Helpers/DashboardPermissions). A antiga tela do Separador (lista de coleções) saiu.
        desktop.MainWindow = new MainWindow
        {
            DataContext = new MainWindowViewModel(),
        };

        VerifyTokenAndValidateDirectory(desktop);
    }

    /// <summary>
    /// Depois que a janela abre, decide se mostra a tela de instruções de atualização
    /// (Helpers/UpdateInstructionsPolicy; arquitetura em docs/arquitetura/tela-instrucoes-atualizacao.html):
    /// - Store + update obrigatório → tela bloqueante (fechar encerra o app)
    /// - Store + update opcional → tela com "Mais tarde"
    /// - Instalador web antigo (Azure) → tela "reinstale pelo letspic.zip", a cada abertura
    /// Para ver a tela sem update real: variável LETSPIC_UPDATE_SCREEN_DEMO = store-required | store-optional | zip.
    /// </summary>
    private static void ScheduleStartupUpdateCheck(IClassicDesktopStyleApplicationLifetime desktop)
    {
        bool checkedByLanding = UpdateInstructionsPolicy.WasCheckedByLanding(desktop.Args);

        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            UpdateInstructionsDecision decision;
            try
            {
                // Fora da thread de UI: a consulta à Store usa rede.
                decision = await Task.Run(() => ResolveStartupUpdateDecisionAsync(checkedByLanding)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SharedClientSide.Helpers.AppInstaller.MsixLog($"Startup update check falhou: {ex.Message}");
                return;
            }

            if (!decision.ShouldShow)
                return;

            await Task.Delay(500);
            await ShowUpdateInstructionsAsync(desktop, decision);
        });
    }

    private static async Task<UpdateInstructionsDecision> ResolveStartupUpdateDecisionAsync(bool checkedByLanding)
    {
        UpdateInstructionsDecision? demo = UpdateInstructionsPolicy.TryParseDemo(
            Environment.GetEnvironmentVariable(UpdateInstructionsPolicy.DemoEnvironmentVariable));
        if (demo != null)
            return demo;

        StoreUpdateService store = StoreUpdateService.Instance;
        InstallSource source = store.GetInstallSource();
        StoreUpdateState state = source == InstallSource.MicrosoftStore
            ? await store.CheckStoreUpdatesAsync().ConfigureAwait(false)
            : StoreUpdateState.None;

        UpdateInstructionsDecision decision = UpdateInstructionsPolicy.Decide(source, state, checkedByLanding);
        SharedClientSide.Helpers.AppInstaller.MsixLog(
            $"Startup update: source={source}, store={state}, checkedByLanding={checkedByLanding}, mode={decision.Mode}, blocking={decision.IsBlocking}");
        return decision;
    }

    private static async Task ShowUpdateInstructionsAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        UpdateInstructionsDecision decision)
    {
        var vm = new UpdateInstructionsViewModel(
            decision,
            StoreUpdateService.Instance.GetInstalledVersionLabel(),
            StoreUpdateService.Instance);
        var window = new UpdateInstructionsWindow { DataContext = vm };

        if (decision.IsBlocking)
        {
            // Versão incompatível: a tela vira a janela principal e fechá-la encerra o app.
            var oldWindow = desktop.MainWindow;
            window.Topmost = true;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Closed += (_, _) => desktop.Shutdown(0);
            desktop.MainWindow = window;
            window.Show();
            try
            {
                (oldWindow?.DataContext as IDisposable)?.Dispose();
                oldWindow?.Close();
            }
            catch { /* janela já pode ter sido fechada */ }
            return;
        }

        // Modal sobre a janela aberta (login ou dashboard), para ser lida antes de seguir.
        Window? owner = desktop.MainWindow;
        if (owner != null && owner.IsVisible)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Show();
        }
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
                isStillValid = lr.loginFailed != true && lr.success
                    && lr.User.loginTokenExpirationDate > DateTime.UtcNow
                    && JwtSessionInfo.IsValidJwt(lr.User.loginToken);
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