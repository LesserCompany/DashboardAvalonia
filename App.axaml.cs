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
    /// Evento disparado quando o idioma é alterado
    /// </summary>
    public static event EventHandler? LanguageChanged;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Carrega as configurações primeiro
        GlobalAppStateViewModel.LoadOptionsModel();
        
        // Configura os delegates para override de endpoint usando o helper compartilhado
        SharedClientSide_AVALONIA.Helpers.EndpointConfigHelper.ConfigureLesserFunctionClientEndpoints();
        
        StartUpLanguageApp();
        StartUpThemeApp();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var lr = LesserFunctionClient.loginFileResult;

            Console.WriteLine("=== VERIFICAÇÃO DE TOKEN INICIAL ===");
            Console.WriteLine($"loginFileResult existe: {lr != null}");
            Console.WriteLine($"loginFileResult.User existe: {lr?.User != null}");
            
            if (lr != null)
            {
                Console.WriteLine($"lr.loginFailed: {lr.loginFailed}");
                Console.WriteLine($"lr.success: {lr.success}");
                if (lr.User != null)
                {
                    Console.WriteLine($"lr.User.loginTokenExpirationDate: {lr.User.loginTokenExpirationDate}");
                    Console.WriteLine($"DateTime.UtcNow: {DateTime.UtcNow}");
                    Console.WriteLine($"Token expirou?: {lr.User.loginTokenExpirationDate <= DateTime.UtcNow}");
                }
            }

            // Validação do token seguindo a mesma lógica do uploader
            bool isValidToken = false;
            if (lr != null && lr.User != null)
            {
                // Replica a verificação do uploader: lr.loginFailed != true && lr.success && lr.User.loginTokenExpirationDate > DateTime.UtcNow
                isValidToken = lr.loginFailed != true && lr.success && lr.User.loginTokenExpirationDate > DateTime.UtcNow;
            }
            
            Console.WriteLine($"Token é válido?: {isValidToken}");
            Console.WriteLine("===================================");

            if (!isValidToken)
            {
                Console.WriteLine("OnFrameworkInitializationCompleted: Token inválido, iniciando janela de login");
                
                // IMPORTANTE: Limpa o arquivo de login inválido
                try
                {
                    if (LesserFunctionClient.loginFileInfo.Exists)
                    {
                        Console.WriteLine("OnFrameworkInitializationCompleted: Removendo arquivo de login inválido");
                        LesserFunctionClient.loginFileInfo.Delete();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OnFrameworkInitializationCompleted: Erro ao remover arquivo de login: {ex.Message}");
                }
                
                // Reseta o LesserFunctionClient para garantir que não use dados inválidos
                GlobalAppStateViewModel.ResetLesserFunctionClient();
                
                // Reaplica as configurações de tema e idioma antes de criar a janela de login
                ReapplySettings();
                
                AuthWindowInstance = new AuthWindow();
                desktop.MainWindow = AuthWindowInstance; // CORREÇÃO: Define como MainWindow
                base.OnFrameworkInitializationCompleted();
                return; // IMPORTANTE: sair aqui se o token for inválido
            }

            // Só inicializa o lfc SE o token for válido
            var lfc = GlobalAppStateViewModel.lfc;
            
            // Verifica novamente se o lfc foi inicializado corretamente
            if (lfc == null || lfc.loginResult == null || lfc.loginResult.User == null)
            {
                Console.WriteLine("OnFrameworkInitializationCompleted: LesserFunctionClient não inicializado, iniciando janela de login");
                
                // IMPORTANTE: Limpa o arquivo de login inválido
                try
                {
                    if (LesserFunctionClient.loginFileInfo.Exists)
                    {
                        Console.WriteLine("OnFrameworkInitializationCompleted: Removendo arquivo de login inválido (lfc não inicializado)");
                        LesserFunctionClient.loginFileInfo.Delete();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OnFrameworkInitializationCompleted: Erro ao remover arquivo de login: {ex.Message}");
                }
                
                // Reseta o LesserFunctionClient para garantir que não use dados inválidos
                GlobalAppStateViewModel.ResetLesserFunctionClient();
                
                // Reaplica as configurações de tema e idioma antes de criar a janela de login
                ReapplySettings();
                
                AuthWindowInstance = new AuthWindow();
                desktop.MainWindow = AuthWindowInstance; // CORREÇÃO: Define como MainWindow
                base.OnFrameworkInitializationCompleted();
                return; // IMPORTANTE: sair aqui se o lfc não foi inicializado
            }

            Console.WriteLine($"OnFrameworkInitializationCompleted: Token válido para usuário tipo '{lr.User.userType}'");
            
            // Verifica o tipo de usuário para determinar qual janela mostrar
            if (lr.User.userType == "professionals")
            {
                // Para separadores, mostra a ProfessionalWindow
                desktop.MainWindow = new Views.ProfessionalWindow.ProfessionalWindowView(lfc);
            }
            else
            {
                // Para empresas, mostra a MainWindow normal
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }
            
            // Verifica o token IMEDIATAMENTE após abrir o dashboard (sem delay)
            // Se o token expirar nesse momento, redireciona para login
            _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                VerifyTokenImmediately(desktop);
            });
            
            // Valida o diretório de downloads após abrir já logado
            _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Aguarda um pouco para garantir que a janela está totalmente carregada
                await Task.Delay(500);
                await GlobalAppStateViewModel.Instance.ValidateAndPromptDownloadDirectoryIfNeeded();
            });
        }
        
        base.OnFrameworkInitializationCompleted(); // CORREÇÃO: Chama apenas uma vez no final
    }
    
    /// <summary>
    /// Verifica o token imediatamente após o dashboard abrir e redireciona se inválido
    /// </summary>
    private static void VerifyTokenImmediately(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            Console.WriteLine("=== VERIFICAÇÃO IMEDIATA DE TOKEN ===");
            Console.WriteLine("VerifyTokenImmediately: Verificando token...");
            
            var lr = LesserFunctionClient.loginFileResult;
            
            Console.WriteLine($"VerifyTokenImmediately: loginFileResult existe: {lr != null}");
            Console.WriteLine($"VerifyTokenImmediately: lr.User existe: {lr?.User != null}");
            
            if (lr != null)
            {
                Console.WriteLine($"VerifyTokenImmediately: lr.loginFailed = {lr.loginFailed}");
                Console.WriteLine($"VerifyTokenImmediately: lr.success = {lr.success}");
            }
            
            // Verifica se o token ainda está válido
            bool isStillValid = false;
            if (lr != null && lr.User != null)
            {
                var expirationDate = lr.User.loginTokenExpirationDate;
                var nowUtc = DateTime.UtcNow;
                Console.WriteLine($"VerifyTokenImmediately: expirationDate = {expirationDate}");
                Console.WriteLine($"VerifyTokenImmediately: nowUtc = {nowUtc}");
                Console.WriteLine($"VerifyTokenImmediately: expirationDate > nowUtc = {expirationDate > nowUtc}");
                Console.WriteLine($"VerifyTokenImmediately: lr.loginFailed != true = {lr.loginFailed != true}");
                Console.WriteLine($"VerifyTokenImmediately: lr.success = {lr.success}");
                
                isStillValid = lr.loginFailed != true && lr.success && expirationDate > nowUtc;
                Console.WriteLine($"VerifyTokenImmediately: isStillValid = {isStillValid}");
            }
            else
            {
                Console.WriteLine("VerifyTokenImmediately: lr ou lr.User é null, token inválido");
            }

            if (!isStillValid)
            {
                Console.WriteLine("VerifyTokenImmediately: Token inválido ou expirado, redirecionando para login...");
                
                // IMPORTANTE: Limpa o arquivo de login expirado
                try
                {
                    if (LesserFunctionClient.loginFileInfo.Exists)
                    {
                        Console.WriteLine("VerifyTokenImmediately: Removendo arquivo de login expirado");
                        LesserFunctionClient.loginFileInfo.Delete();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"VerifyTokenImmediately: Erro ao remover arquivo de login: {ex.Message}");
                }
                
                // Reseta o LesserFunctionClient
                GlobalAppStateViewModel.ResetLesserFunctionClient();
                
                // Redireciona para login SEM delay
                RedirectToLoginWithMessage(desktop);
            }
            else
            {
                Console.WriteLine("VerifyTokenImmediately: Token válido, dashboard pode continuar");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar token imediatamente: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Redireciona para login com mensagem informando que o token está inválido/expirado
    /// </summary>
    private static void RedirectToLoginWithMessage(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            Console.WriteLine("RedirectToLoginWithMessage: Iniciando redirecionamento com mensagem");
            
            // Salva referência da janela antiga
            var oldWindow = desktop.MainWindow;
            
            // Reaplica as configurações de tema e idioma
            ReapplySettings();
            
            // Cria a nova janela de login
            AuthWindowInstance = new AuthWindow();
            
            // Define a nova janela como MainWindow
            desktop.MainWindow = AuthWindowInstance;
            
            // Mostra a nova janela
            AuthWindowInstance.Show();
            
            // Mostra mensagem ao usuário informando que o token está inválido/expirado
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
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao mostrar mensagem: {ex.Message}");
                }
            });
            
            // Fecha a janela antiga (se existir)
            if (oldWindow != null)
            {
                Console.WriteLine("RedirectToLoginWithMessage: Fechando janela antiga");
                // Limpa o DataContext antes de fechar
                (oldWindow.DataContext as IDisposable)?.Dispose();
                oldWindow.Close();
            }
            
            Console.WriteLine("RedirectToLoginWithMessage: Redirecionamento concluído");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao redirecionar com mensagem: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Mostra mensagem informando que o token expirou
    /// </summary>
    private static async Task ShowTokenExpiredMessage()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var box = MessageBoxManager.GetMessageBoxStandard(
                    Loc.Tr("Session expired"),
                    Loc.Tr("Your session has expired. Please login again."),
                    MsBox.Avalonia.Enums.ButtonEnum.Ok
                );
                
                if (desktop.MainWindow != null)
                {
                    await box.ShowWindowDialogAsync(desktop.MainWindow);
                }
                else
                {
                    await box.ShowAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao mostrar mensagem de token expirado: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Redireciona o usuário para a tela de login
    /// </summary>
    public static void RedirectToLoginScreen()
    {
        try
        {
            // Evita redirecionamento duplo
            if (isRedirecting)
            {
                Console.WriteLine("RedirectToLoginScreen: Já redirecionando, ignorando chamada duplicada");
                return;
            }
            
            isRedirecting = true;
            Console.WriteLine("RedirectToLoginScreen: Iniciando redirecionamento para login");
            
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Salva referência da janela antiga
                var oldWindow = desktop.MainWindow;
                
                // Verifica se já não é a janela de login
                if (oldWindow is AuthWindow)
                {
                    Console.WriteLine("RedirectToLoginScreen: Já está na janela de login, ignorando");
                    isRedirecting = false;
                    return;
                }
                
                // Reaplica as configurações de tema e idioma
                ReapplySettings();
                
                // Cria a nova janela de login
                AuthWindowInstance = new AuthWindow();
                
                // Define a nova janela como MainWindow ANTES de mostrar
                desktop.MainWindow = AuthWindowInstance;
                
                // Mostra a nova janela
                AuthWindowInstance.Show();
                
                // Agora fecha a janela antiga (se existir)
                if (oldWindow != null)
                {
                    Console.WriteLine("RedirectToLoginScreen: Fechando janela antiga");
                    // Limpa o DataContext antes de fechar
                    (oldWindow.DataContext as IDisposable)?.Dispose();
                    oldWindow.Close();
                }
                
                Console.WriteLine("RedirectToLoginScreen: Redirecionamento concluído");
            }
            
            // Reseta o flag após um delay
            _ = Task.Delay(2000).ContinueWith(_ => {
                isRedirecting = false;
                Console.WriteLine("RedirectToLoginScreen: Flag de redirecionamento resetado");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao redirecionar para tela de login: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            isRedirecting = false;
        }
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
        try
        {
            Console.WriteLine("App.ReapplySettings: Iniciando reaplicação de configurações...");
            
            // Recarrega as configurações do arquivo
            GlobalAppStateViewModel.LoadOptionsModel();
            
            Console.WriteLine($"App.ReapplySettings: Configurações carregadas - Tema: '{GlobalAppStateViewModel.options.AppTheme}', Idioma: '{GlobalAppStateViewModel.options.Language}'");
            
            // Reaplica o tema
            var app = Application.Current;
            if (app != null)
            {
                string savedTheme = GlobalAppStateViewModel.options.AppTheme;
                Console.WriteLine($"App.ReapplySettings: Aplicando tema: '{savedTheme}'");
                
                // Se não há tema salvo, usa o tema padrão do sistema
                if (string.IsNullOrEmpty(savedTheme))
                {
                    savedTheme = "Default";
                }
                
                switch (savedTheme)
                {
                    case "DarkMode":
                        app.RequestedThemeVariant = ThemeVariant.Dark;
                        GlobalAppStateViewModel.Instance.AppIsDarkMode = true;
                        Console.WriteLine("App.ReapplySettings: Tema escuro aplicado");
                        break;
                    case "LightMode":
                        app.RequestedThemeVariant = ThemeVariant.Light;
                        GlobalAppStateViewModel.Instance.AppIsDarkMode = false;
                        Console.WriteLine("App.ReapplySettings: Tema claro aplicado");
                        break;
                    default:
                        app.RequestedThemeVariant = ThemeVariant.Default;
                        // Para tema padrão, verifica o tema atual da aplicação
                        GlobalAppStateViewModel.Instance.AppIsDarkMode = app.ActualThemeVariant == ThemeVariant.Dark;
                        Console.WriteLine("App.ReapplySettings: Tema padrão aplicado");
                        break;
                }
            }
            
            // Reaplica o idioma
            string savedLanguage = GlobalAppStateViewModel.options.Language;
            if (!string.IsNullOrEmpty(savedLanguage))
            {
                Console.WriteLine($"App.ReapplySettings: Aplicando idioma: '{savedLanguage}'");
                SetCurrentLang(savedLanguage);
                Console.WriteLine($"App.ReapplySettings: Idioma aplicado: '{GetCurrentLang()}'");
            }
            else
            {
                Console.WriteLine("App.ReapplySettings: Nenhum idioma salvo encontrado, usando padrão");
                SetCurrentLang(); // Aplica idioma padrão
            }
            
            // CORREÇÃO: Sincroniza as propriedades do GlobalAppStateViewModel com as configurações aplicadas
            // Isso garante que a UI sempre reflita as configurações corretas
            GlobalAppStateViewModel.Instance.AppLanguage = GetCurrentLang();
            
            Console.WriteLine($"App.ReapplySettings: Propriedades sincronizadas - AppIsDarkMode: {GlobalAppStateViewModel.Instance.AppIsDarkMode}, AppLanguage: {GlobalAppStateViewModel.Instance.AppLanguage}");
            Console.WriteLine("App.ReapplySettings: Reaplicação de configurações concluída");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"App.ReapplySettings: Erro ao reaplicar configurações: {ex.Message}");
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
        
        // CORREÇÃO: Garante que a propriedade AppLanguage seja sincronizada na inicialização
        GlobalAppStateViewModel.Instance.AppLanguage = GetCurrentLang();
    }
    public static void SetCurrentLang(string? language = "")
    {
        string previousLang = GetCurrentLang();
        
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
        
        // Se o idioma mudou, limpar cache de combos para forçar recarregamento
        if (previousLang != GetCurrentLang())
        {
            Console.WriteLine($"App: Idioma mudou de '{previousLang}' para '{GetCurrentLang()}', limpando cache de combos");
            Services.ComboPriceService.ClearCache();
            
            // Notificar que os combos precisam ser recarregados
            NotifyLanguageChanged();
        }
    }
    
    /// <summary>
    /// Notifica que o idioma foi alterado
    /// </summary>
    private static void NotifyLanguageChanged()
    {
        try
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
            Console.WriteLine("App: Evento LanguageChanged disparado");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"App: Erro ao disparar evento LanguageChanged: {ex.Message}");
        }
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
        
        // CORREÇÃO: Garante que a propriedade AppIsDarkMode seja sincronizada na inicialização
        string savedTheme = GlobalAppStateViewModel.options.AppTheme;
        switch (savedTheme)
        {
            case "DarkMode":
                GlobalAppStateViewModel.Instance.AppIsDarkMode = true;
                break;
            case "LightMode":
                GlobalAppStateViewModel.Instance.AppIsDarkMode = false;
                break;
            default:
                // Para tema padrão, verifica o tema atual da aplicação
                GlobalAppStateViewModel.Instance.AppIsDarkMode = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                break;
        }
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