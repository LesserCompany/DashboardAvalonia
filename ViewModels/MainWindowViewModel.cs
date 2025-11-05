using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.ViewModels.Collections;
using LesserDashboardClient.ViewModels.Options;
using LesserDashboardClient.Views;
using MsBox.Avalonia;
using SharedClientSide.ServerInteraction;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public static MainWindowViewModel Instance { get; set; }
    public string AppVersion
    {
        get
        {
            return $"v{GetParentFolderName()}";
        }
    }
    public string Greeting { get; } = "Welcome to Avalonia!";
    public string CompanyName => GlobalAppStateViewModel.lfc.loginResult.User.company;
    [ObservableProperty] public  int progressBarUpdanteComponents = 10;
    [ObservableProperty] public bool isUpdateSomeOneComponent = false;

    [ObservableProperty]
    public bool isLoadingEndpointsInfo = false;

    [ObservableProperty]
    public string appMode;

    [ObservableProperty]
    public string titleApp;

    public MainWindowViewModel()
    {
        Instance = this;
        
        // Inicializa o AppMode baseado na configuração de compilação (Alpha, Beta, Debug, Prod)
        string config = LesserFunctionClient.GetConfig();
        AppMode = config;
        
        // Define o título da janela com o modo de build
        TitleApp = $"LetsPic Lesser Client - {AppVersion} - {AppMode}";
    }
    private string GetParentFolderName()
    {
        try
        {
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo? parentDirectory = Directory.GetParent(currentDirectory);
            if (parentDirectory != null)
            {
                return parentDirectory.Name;
            }
            return "...";

        }
        catch (Exception e)
        {
            return "...";
        }
    }
    public async void UpdateProgressBarUpdateComponent(int value)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (value > 0 && value < 100)
                {
                    IsUpdateSomeOneComponent = true;
                }
                else
                {
                    IsUpdateSomeOneComponent = false;
                    return;
                }

                ProgressBarUpdanteComponents = Math.Max(0, Math.Min(100, value));

            });
        }
        catch(Exception e)
        {
            Dispatcher.UIThread.Invoke(() => 
            {
                IsUpdateSomeOneComponent = false;
                ProgressBarUpdanteComponents = 0;
                if (MainWindow.instance != null)
                {
                    var bbox = MessageBoxManager.GetMessageBoxStandard("", $"{e.Message} | {e.StackTrace}");
                    var response = bbox.ShowWindowDialogAsync(MainWindow.instance);
                }
            });
        }
    }
    [RelayCommand]
    public void OpenWhatsAppSuportCommand()
    {
        string companyName = GlobalAppStateViewModel.lfc.loginResult.User.company;
        var url = $"https://wa.me/5518996880201?text=Olá! Falo em nome da empresa {companyName} e preciso de ajuda.";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Aqui você pode logar ou mostrar uma mensagem de erro
            Console.WriteLine($"Erro ao abrir link: {ex.Message}");
        }
    }
    [RelayCommand]
    public async Task LogoutAndExitCommand()
    {
        File.Delete(LesserFunctionClient.loginFileInfo.FullName);
        
        // Reseta a instância estática do LesserFunctionClient
        GlobalAppStateViewModel.ResetLesserFunctionClient();
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });
    }
    [RelayCommand]
    public async Task LogoutCommand()
    {
        try
        {
            // Remove o arquivo de login para limpar as credenciais
            File.Delete(LesserFunctionClient.loginFileInfo.FullName);
            
            // Reseta a instância estática do LesserFunctionClient
            GlobalAppStateViewModel.ResetLesserFunctionClient();
            
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    if (desktop.MainWindow != null)
                    {
                        var oldWindow = desktop.MainWindow;
                        
                        // Reaplica as configurações de tema e idioma antes de criar a nova janela de login
                        App.ReapplySettings();
                        
                        // Aguarda um pouco para garantir que as configurações sejam aplicadas
                        await Task.Delay(100);
                        
                        // Cria e mostra uma nova janela de login
                        var authWindow = new AuthWindow();
                        App.AuthWindowInstance = authWindow;
                        authWindow.Show();
                        
                        // Define a janela de login como a janela principal
                        desktop.MainWindow = authWindow;
                        
                        // Fecha a janela antiga
                        (oldWindow?.DataContext as IDisposable)?.Dispose();
                        oldWindow?.Close();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro durante logout: {ex.Message}");
            // Em caso de erro, usa o método original como fallback
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    if(desktop.MainWindow != null)
                        desktop.MainWindow.Close();
                    App.StartAuthWindow();
                }
            });
        }
    }

    [RelayCommand]
    public async Task GoBackToPreviousVersionCommand()
    {
        try
        {
            // Mostrar confirmação antes de fechar
            var result = await GlobalAppStateViewModel.Instance.ShowDialogYesNo(
                Loc.Tr("Do you really want to go back to the previous version of the dashboard? The current application will be closed."),
                Loc.Tr("Confirmation"));
            
            if (result == true)
            {
                // Tentar abrir a versão anterior do LesserDashboard (WPF)
                await StartPreviousVersionDashboard();
                
                // Fechar a aplicação atual
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        desktop.Shutdown();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            var errorBox = MessageBoxManager.GetMessageBoxStandard(
                "Erro", 
                $"Erro ao tentar abrir a versão anterior: {ex.Message}",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error);
            await errorBox.ShowWindowDialogAsync(MainWindow.instance);
        }
    }

    private async Task StartPreviousVersionDashboard()
    {
        try
        {
            // Caminho para as versões do LesserDashboard (WPF) conforme informado pelo usuário
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appVersionsPath = Path.Combine(documentsPath, "Separacao", "apps", "LesserDashboard", "v");
            
            string previousVersionPath = "";
            
            if (Directory.Exists(appVersionsPath))
            {
                // Buscar todas as versões disponíveis, excluindo a versão atual
                var versionDirs = Directory.GetDirectories(appVersionsPath)
                    .Where(d => !d.EndsWith(GetParentFolderName())) // Excluir a versão atual
                    .OrderByDescending(d => d) // Ordenar por nome (versão mais recente primeiro)
                    .ToArray();
                
                if (versionDirs.Length > 0)
                {
                    // Pegar a versão mais recente disponível (que não seja a atual)
                    previousVersionPath = Path.Combine(versionDirs[0], "LesserDashboard.exe");
                }
            }

            if (File.Exists(previousVersionPath))
            {
                // Abrir diretamente com Process.Start usando o caminho encontrado
                Process.Start(new ProcessStartInfo
                {
                    FileName = previousVersionPath,
                    UseShellExecute = true
                });
            }
            else
            {
                // Fallback: tentar usar AppInstaller se não encontrar versão específica
                SharedClientSide.Helpers.AppInstaller installer = new SharedClientSide.Helpers.AppInstaller("LesserDashboard", _ => { });
                await installer.startApp();
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Não foi possível localizar ou executar a versão anterior do dashboard: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ShowEndpointsInfoCommand()
    {
        try
        {
            IsLoadingEndpointsInfo = true;
            
            string config = LesserFunctionClient.GetConfig();
            string functionAppEndpoint = await LesserFunctionClient.GetFunctionAppEndpoint();
            string gcloudAppEndpoint = await LesserFunctionClient.GetGCloudAppEndpoint();

            // Esconde o loading assim que os dados são obtidos, antes de mostrar o MessageBox
            IsLoadingEndpointsInfo = false;

            string message = $"Modo: {config}\n\n" +
                           $"FunctionAppEndpoint utilizado:\n{functionAppEndpoint}\n\n" +
                           $"GcloudAppEndpoint utilizado:\n{gcloudAppEndpoint}";

            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "Informações de Endpoints",
                message,
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Info
            );

            if (MainWindow.instance != null)
            {
                await messageBox.ShowWindowDialogAsync(MainWindow.instance);
            }
            else
            {
                await messageBox.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            // Esconde o loading em caso de erro também
            IsLoadingEndpointsInfo = false;
            
            var errorBox = MessageBoxManager.GetMessageBoxStandard(
                "Erro",
                $"Erro ao obter informações dos endpoints: {ex.Message}",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error
            );
            
            if (MainWindow.instance != null)
            {
                await errorBox.ShowWindowDialogAsync(MainWindow.instance);
            }
            else
            {
                await errorBox.ShowAsync();
            }
        }
    }

}