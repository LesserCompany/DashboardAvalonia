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
using LesserDashboardClient.Views;
using LesserDashboardClient.Helpers;
using MsBox.Avalonia;
using Newtonsoft.Json;
using SharedClientSide.Helpers;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Login;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient;

public partial class App : Application
{
    /// <summary>Product Id na Microsoft Store — https://apps.microsoft.com/detail/9P5GDKBRXR16</summary>
    private const string MsixStoreProductId = "9P5GDKBRXR16";

    /// <summary>Se true, mostra a janela de "atualização disponível" (fase 1 - só avisar) no startup para demo. Colocar false em produção.</summary>
    private const bool ForceShowUpdateAvailableForDemo = true;
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

            // Demo fase 2: bloqueia imediatamente (sem rede).
            if (ForceShowUpdateRequiredForDemo)
            {
                if (MsixStoreUpdateChecker.IsValidStoreProductId(MsixStoreProductId))
                    MsixStoreUpdateChecker.OpenMicrosoftStore(MsixStoreProductId);
                ShowUpdateRequiredWindowAndShutdown(desktop, InstallChannel.StoreMsix);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // Abrir UI primeiro — nunca bloquear a thread de UI com GetResult() no check de update.
            // O check (MSIX Store / sideload / web VersioningParams) corre async depois da janela.
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
            isValidToken = lr.loginFailed != true && lr.success && lr.User.loginTokenExpirationDate > DateTime.UtcNow;
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

        VerifyTokenAndValidateDirectory(desktop);
    }

    /// <summary>
    /// Check de update pós-UI:
    /// - MSIX Required → troca para janela de bloqueio e shutdown
    /// - MSIX/Web Available → aviso fase 1 por cima
    /// - Sem update / erro / debug VS → segue normal (UI já aberta)
    /// </summary>
    private static void ScheduleStartupUpdateCheck(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (ForceShowUpdateAvailableForDemo)
        {
            _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(500);
                ShowUpdateAvailableNotice(new AppUpdateCheckOutcome
                {
                    Status = AppUpdateCheckStatus.Available,
                    Channel = PackagedAppHelper.GetInstallChannel()
                });
            });
        }

        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            AppUpdateCheckOutcome? updateOutcome = null;
            try
            {
                // Off UI thread: evita deadlock com SyncContext do Avalonia e não trava a janela.
                updateOutcome = await Task.Run(async () =>
                    await AppUpdateOrchestrator.CheckOnStartupAsync(
                        MsixStoreProductId,
                        "LesserDashboard",
                        openStoreIfNeeded: true).ConfigureAwait(false)).ConfigureAwait(true);

                SharedClientSide.Helpers.AppInstaller.MsixLog(
                    $"Startup update: channel={updateOutcome.Channel}, status={updateOutcome.Status}, detail={updateOutcome.Detail}");
            }
            catch (Exception ex)
            {
                SharedClientSide.Helpers.AppInstaller.MsixLog($"Startup update check falhou: {ex.Message}");
                return;
            }

            if (updateOutcome == null)
                return;

            if (updateOutcome.Status == AppUpdateCheckStatus.Required)
            {
                if (!updateOutcome.OpenedMicrosoftStore
                    && updateOutcome.Channel != InstallChannel.WebLegacy
                    && MsixStoreUpdateChecker.IsValidStoreProductId(MsixStoreProductId))
                {
                    MsixStoreUpdateChecker.OpenMicrosoftStore(MsixStoreProductId);
                }

                var oldWindow = desktop.MainWindow;
                ShowUpdateRequiredWindowAndShutdown(desktop, updateOutcome.Channel);
                try
                {
                    oldWindow?.Close();
                }
                catch { /* janela já pode ter sido fechada */ }
                return;
            }

            if (updateOutcome.Status == AppUpdateCheckStatus.Available)
            {
                // Em DEBUG/VS o exe está em bin/Debug — path ≠ pasta do servidor → falso positivo.
                if (updateOutcome.Channel == InstallChannel.WebLegacy && ShouldSuppressWebLegacyUpdateNotice())
                {
                    SharedClientSide.Helpers.AppInstaller.MsixLog(
                        "Startup update: Available (WebLegacy) ignorado — build de desenvolvimento/debug.");
                    return;
                }

                await Task.Delay(500);
                ShowUpdateAvailableNotice(updateOutcome);
            }
        });
    }

    /// <summary>
    /// Evita aviso falso de "versão nova no servidor" ao correr F5 / bin\Debug|Release.
    /// Instalação web real (Documents/Separacao/apps) continua a ver o aviso.
    /// </summary>
    private static bool ShouldSuppressWebLegacyUpdateNotice()
    {
#if DEBUG
        return true;
#else
        if (Debugger.IsAttached)
            return true;

        try
        {
            string dir = Path.GetFullPath(AppContext.BaseDirectory);
            return dir.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                   || dir.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
#endif
    }

    private static IBrush GetAccentBrush()
    {
        if (Current?.TryGetResource("SystemControlBackgroundAccentBrush", Current.ActualThemeVariant, out var res) == true
            && res is IBrush brush)
            return brush;
        return new SolidColorBrush(Color.Parse("#E67E22"));
    }

    private static Button CreatePrimaryDialogButton(string text, double minWidth = 132)
    {
        return new Button
        {
            Content = text,
            MinWidth = minWidth,
            MinHeight = 40,
            Padding = new Thickness(18, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = GetAccentBrush(),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
    }

    private static Button CreateSecondaryDialogButton(string text, double minWidth = 132)
    {
        return new Button
        {
            Content = text,
            MinWidth = minWidth,
            MinHeight = 40,
            Padding = new Thickness(18, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#3A3A3E")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#55555A")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
    }

    private static Window BuildUpdateDialogWindow(
        string title,
        string subtitle,
        string message,
        Control buttons,
        bool topmost = false)
    {
        var header = new Border
        {
            Background = GetAccentBrush(),
            Padding = new Thickness(22, 16),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = Brushes.White,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = subtitle,
                        FontSize = 13,
                        Foreground = Brushes.White,
                        Opacity = 0.92,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

        var body = new StackPanel
        {
            Margin = new Thickness(22, 18, 22, 20),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    FontSize = 14,
                    LineHeight = 22,
                    Foreground = new SolidColorBrush(Color.Parse("#E8E8EA")),
                    TextWrapping = TextWrapping.Wrap
                },
                buttons
            }
        };

        return new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            MinHeight = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = topmost,
            Background = new SolidColorBrush(Color.Parse("#2B2B2F")),
            Content = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children =
                {
                    header,
                    new Border
                    {
                        [Grid.RowProperty] = 1,
                        Child = body
                    }
                }
            }
        };
    }

    /// <summary>
    /// Fase 1 — Só avisar (docs Microsoft: update disponível, utilizador decide).
    /// "Atualizar agora" abre a Store; "Mais tarde" continua a usar a app.
    /// </summary>
    private static void ShowUpdateAvailableNotice(AppUpdateCheckOutcome? outcome)
    {
        InstallChannel channel = outcome?.Channel ?? PackagedAppHelper.GetInstallChannel();
        bool isWeb = channel == InstallChannel.WebLegacy;
        string message = isWeb
            ? "Há uma versão mais recente do aplicativo no servidor. Pode continuar a usar esta versão ou atualizar quando quiser."
            : "Há uma atualização disponível na Microsoft Store. Pode continuar a usar a aplicação agora e atualizar quando quiser.";

        var btnUpdate = CreatePrimaryDialogButton(isWeb ? "Entendi" : "Atualizar agora");
        var btnLater = CreateSecondaryDialogButton("Mais tarde");
        btnLater.IsVisible = !isWeb;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { btnLater, btnUpdate }
        };

        var window = BuildUpdateDialogWindow(
            title: "Atualização disponível",
            subtitle: isWeb ? "Nova versão no servidor" : "Atualização na Microsoft Store",
            message: message,
            buttons: buttons,
            topmost: true);

        btnUpdate.Click += (_, _) =>
        {
            if (!isWeb && MsixStoreUpdateChecker.IsValidStoreProductId(MsixStoreProductId))
                MsixStoreUpdateChecker.OpenMicrosoftStore(MsixStoreProductId);
            window.Close();
        };
        btnLater.Click += (_, _) => window.Close();
        window.Show();
    }

    /// <summary>
    /// Fase 2 — Bloquear (docs Microsoft: Mandatory / Required — app pode terminar se o utilizador não atualizar).
    /// Não abre o dashboard; ao fechar encerra o processo.
    /// </summary>
    private static void ShowUpdateRequiredWindowAndShutdown(
        IClassicDesktopStyleApplicationLifetime desktop,
        InstallChannel channel)
    {
        bool isWeb = channel == InstallChannel.WebLegacy;
        string message = isWeb
            ? "Existe uma atualização obrigatória. Atualize pelo instalador web e volte a abrir a aplicação."
            : "Existe uma atualização obrigatória. A Microsoft Store foi aberta. Instale a atualização e volte a abrir a aplicação — esta versão não pode continuar.";

        var btnClose = CreateSecondaryDialogButton("Fechar aplicação", 148);
        var btnStore = CreatePrimaryDialogButton("Abrir Microsoft Store", 168);
        btnStore.IsVisible = !isWeb;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { btnClose, btnStore }
        };

        var window = BuildUpdateDialogWindow(
            title: "Atualização obrigatória",
            subtitle: "É necessário atualizar para continuar",
            message: message,
            buttons: buttons);

        window.Closed += (_, _) => desktop.Shutdown(0);
        btnClose.Click += (_, _) => window.Close();
        btnStore.Click += (_, _) =>
        {
            if (MsixStoreUpdateChecker.IsValidStoreProductId(MsixStoreProductId))
                MsixStoreUpdateChecker.OpenMicrosoftStore(MsixStoreProductId);
        };
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