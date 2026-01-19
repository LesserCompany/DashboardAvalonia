using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.Models;
using SharedClientSide.ServerInteraction.Users.Results;
using LesserDashboardClient.ViewModels.Collections;
using LesserDashboardClient.ViewModels.Options;
using LesserDashboardClient.Views;
using MsBox.Avalonia;
using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private int unreadMessagesCount = 0;

    [ObservableProperty]
    private ObservableCollection<UserMessage> userMessages = new();

    /// <summary>
    /// Indica se as mensagens já foram carregadas da API (mesmo que vazias)
    /// </summary>
    private bool _messagesLoaded = false;
    
    /// <summary>
    /// Guarda o número de mensagens não lidas antes da última verificação (para detectar novas mensagens)
    /// </summary>
    private int _previousUnreadMessagesCount = 0;
    
    /// <summary>
    /// Indica se as mensagens já foram carregadas da API
    /// </summary>
    public bool MessagesLoaded => _messagesLoaded;

    [ObservableProperty]
    private int selectedTabIndex = 0;

    public bool HasUnreadMessages => UnreadMessagesCount > 0;

    /// <summary>
    /// Mensagens ordenadas: não lidas primeiro, depois por data decrescente
    /// </summary>
    public IEnumerable<UserMessage> SortedUserMessages => UserMessages
        .OrderBy(m => m.IsRead)
        .ThenByDescending(m => m.CreatedDate);

    public MainWindowViewModel()
    {
        Instance = this;
        
        // Inicializa o AppMode baseado na configuração de compilação (Alpha, Beta, Debug, Prod)
        string config = LesserFunctionClient.GetConfig();
        AppMode = config;
        
        // Define o título da janela com o modo de build
        TitleApp = $"LetsPic Lesser Client - {AppVersion} - {AppMode}";
        
        // Carrega mensagens do usuário ao inicializar
        _ = LoadUserMessagesAsync();
        
        // Atualiza periodicamente as mensagens (a cada 30 segundos)
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        timer.Tick += async (s, e) => await LoadUserMessagesAsync();
        timer.Start();
    }
    
    partial void OnUnreadMessagesCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnreadMessages));
        // Notifica que o estado de mensagens não lidas mudou
        OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
    }

    partial void OnUserMessagesChanged(ObservableCollection<UserMessage> value)
    {
        OnPropertyChanged(nameof(SortedUserMessages));
        // Notifica que o estado de mensagens não lidas mudou
        OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
    }

    /// <summary>
    /// Indica se deve mostrar a tela de mensagens na inicialização
    /// (true se houver pelo menos uma mensagem não lida)
    /// Retorna false em caso de erro ou se não houver mensagens carregadas
    /// </summary>
    public bool ShouldShowMessagesOnStartup
    {
        get
        {
            try
            {
                // Se UserMessages for null ou vazio, não mostrar mensagens (mostrar welcome)
                if (UserMessages == null || UserMessages.Count == 0)
                {
                    return false;
                }
                
                // Verificar se há mensagens não lidas
                return HasUnreadMessages;
            }
            catch
            {
                // Em caso de qualquer erro, mostrar welcome por padrão
                return false;
            }
        }
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
                        
                        // NÃO reinicializar configurações - elas já estão aplicadas
                        // As configurações só devem ser alteradas manualmente pelo usuário nas Opções
                        
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
                // Versão hardcoded: 188
                SharedClientSide.Helpers.AppInstaller installer = new SharedClientSide.Helpers.AppInstaller("LesserDashboard", _ => { }, "188");
                await installer.startApp();
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Não foi possível localizar ou executar a versão anterior do dashboard: {ex.Message}");
        }
    }

    /// <summary>
    /// Carrega as mensagens não lidas do usuário do servidor
    /// A lógica de verificar se precisa carregar está aqui, não na View
    /// </summary>
    public async Task LoadUserMessagesAsync(bool forceReload = false)
    {
        try
        {
            // Regra de negócio: só recarrega se forçado ou se não houver mensagens
            if (!forceReload && UserMessages != null && UserMessages.Count > 0)
            {
                return;
            }

            // Verificar se o cliente está disponível
            if (GlobalAppStateViewModel.lfc == null)
            {
                Console.WriteLine("LoadUserMessagesAsync: LesserFunctionClient não está disponível");
                // Garantir que ShouldShowMessagesOnStartup retorne false em caso de erro
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _messagesLoaded = true; // Marcar como carregado mesmo com erro (para não ficar esperando)
                    OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
                    OnPropertyChanged(nameof(MessagesLoaded));
                });
                return;
            }

            var result = await GlobalAppStateViewModel.lfc.GetUserNotifications<UserMessage>();
            
            // Tratar casos de erro ou null
            if (result == null)
            {
                Console.WriteLine("LoadUserMessagesAsync: Resultado da API é null");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Limpar mensagens existentes se houver erro
                    UserMessages?.Clear();
                    UnreadMessagesCount = 0;
                    _messagesLoaded = true; // Marcar como carregado mesmo com erro (para não ficar esperando)
                    OnPropertyChanged(nameof(SortedUserMessages));
                    OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
                    OnPropertyChanged(nameof(MessagesLoaded));
                });
                return;
            }

            if (!result.success)
            {
                Console.WriteLine($"LoadUserMessagesAsync: API retornou erro - {result.message ?? "Erro desconhecido"}");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Limpar mensagens existentes se houver erro
                    UserMessages?.Clear();
                    UnreadMessagesCount = 0;
                    _messagesLoaded = true; // Marcar como carregado mesmo com erro (para não ficar esperando)
                    OnPropertyChanged(nameof(SortedUserMessages));
                    OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
                    OnPropertyChanged(nameof(MessagesLoaded));
                });
                return;
            }

            if (result.Content == null)
            {
                Console.WriteLine("LoadUserMessagesAsync: Content da resposta é null");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Limpar mensagens existentes se houver erro
                    UserMessages?.Clear();
                    UnreadMessagesCount = 0;
                    _messagesLoaded = true; // Marcar como carregado mesmo com erro (para não ficar esperando)
                    OnPropertyChanged(nameof(SortedUserMessages));
                    OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
                    OnPropertyChanged(nameof(MessagesLoaded));
                });
                return;
            }
            
            // Sucesso: processar mensagens
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    UserMessages.Clear();
                    
                    foreach (var message in result.Content)
                    {
                        // Garantir que MessageType tenha um valor padrão se não vier da API
                        if (string.IsNullOrEmpty(message.MessageType))
                        {
                            message.MessageType = "info";
                        }
                        
                        UserMessages.Add(message);
                    }
                    
                    // Atualiza a contagem de mensagens não lidas
                    UnreadMessagesCount = UserMessages.Count(m => !m.IsRead);
                    
                    // Marcar que as mensagens foram carregadas (mesmo que vazias)
                    _messagesLoaded = true;
                    
                    // Notifica mudança na coleção ordenada
                    OnPropertyChanged(nameof(SortedUserMessages));
                    
                    // Notifica mudança no estado de mensagens não lidas (para decidir view inicial)
                    OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
                    OnPropertyChanged(nameof(MessagesLoaded));
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"Erro ao processar mensagens: {innerEx.Message}");
                    // Em caso de erro ao processar, garantir estado seguro
                    UnreadMessagesCount = 0;
                    _messagesLoaded = true; // Marcar como carregado mesmo com erro (para não ficar esperando)
                    OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
                    OnPropertyChanged(nameof(MessagesLoaded));
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar mensagens do usuário: {ex.Message}");
            // Em caso de exceção, garantir que ShouldShowMessagesOnStartup retorne false
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UserMessages?.Clear();
                    UnreadMessagesCount = 0;
                    _messagesLoaded = true; // Marcar como carregado mesmo com erro (para não ficar esperando)
                    OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
                    OnPropertyChanged(nameof(MessagesLoaded));
                });
            }
            catch
            {
                // Se nem isso funcionar, pelo menos logar
                Console.WriteLine("Erro crítico ao limpar estado de mensagens após exceção");
            }
        }
    }

    /// <summary>
    /// Marca uma mensagem específica como lida
    /// </summary>
    [RelayCommand]
    public async Task MarkMessageAsReadAsync(string messageId)
    {
        try
        {
            var message = UserMessages.FirstOrDefault(m => m.Id == messageId);
            if (message != null && !message.IsRead)
            {
                // Chama o endpoint para marcar como visualizada no servidor
                var result = await GlobalAppStateViewModel.lfc.MarkUserNotificationAsViewed(messageId);
                
                if (result != null && result.success)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        message.IsRead = true;
                        UnreadMessagesCount = UserMessages.Count(m => !m.IsRead);
                        // Notifica mudança na coleção ordenada
                        OnPropertyChanged(nameof(SortedUserMessages));
                        // Notifica mudança no estado de mensagens não lidas
                        OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao marcar mensagem como lida: {ex.Message}");
        }
    }

    /// <summary>
    /// Marca todas as mensagens como lidas
    /// </summary>
    [RelayCommand]
    public async Task MarkAllMessagesAsReadAsync()
    {
        try
        {
            var unreadMessages = UserMessages.Where(m => !m.IsRead).ToList();
            
            // Marca cada mensagem não lida no servidor
            foreach (var message in unreadMessages)
            {
                var result = await GlobalAppStateViewModel.lfc.MarkUserNotificationAsViewed(message.Id);
                if (result != null && result.success)
                {
                    message.IsRead = true;
                }
            }
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UnreadMessagesCount = UserMessages.Count(m => !m.IsRead);
                // Notifica mudança na coleção ordenada
                OnPropertyChanged(nameof(SortedUserMessages));
                // Notifica mudança no estado de mensagens não lidas
                OnPropertyChanged(nameof(ShouldShowMessagesOnStartup));
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao marcar todas mensagens como lidas: {ex.Message}");
        }
    }

    /// <summary>
    /// Comando para abrir a página de mensagens do usuário
    /// </summary>
    [RelayCommand]
    public void OpenMessagesCommand()
    {
        try
        {
            // Navega para a aba de Collections (índice 0)
            SelectedTabIndex = 0;
            
            // Aguarda um pouco para garantir que a aba foi trocada antes de mudar a view
            Dispatcher.UIThread.Post(() =>
            {
                // Abre a view de mensagens
                var collectionsViewModel = CollectionsViewModel.Instance;
                if (collectionsViewModel != null)
                {
                    collectionsViewModel.ActiveComponent = CollectionsViewModel.ActiveViews.MessagesView;
                }
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao abrir mensagens: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica se há novas mensagens não lidas após criar uma turma.
    /// Se houver MAIS mensagens não lidas do que antes, abre o componente de mensagens.
    /// Se o número for o mesmo ou menor, não abre.
    /// </summary>
    public async Task CheckForNewMessagesAfterCollectionCreationAsync()
    {
        try
        {
            // Guarda o número atual de mensagens não lidas antes de recarregar
            int previousCount = UnreadMessagesCount;
            
            // Recarrega as mensagens do servidor
            await LoadUserMessagesAsync();
            
            // Após carregar, verifica se há MAIS mensagens não lidas do que antes
            int newCount = UnreadMessagesCount;
            
            if (newCount > previousCount)
            {
                // Há novas mensagens! Abre o componente de mensagens
                Console.WriteLine($"Novas mensagens detectadas: {previousCount} -> {newCount}. Abrindo componente de mensagens.");
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Navega para a aba de Collections (índice 0)
                    SelectedTabIndex = 0;
                    
                    // Abre a view de mensagens
                    var collectionsViewModel = CollectionsViewModel.Instance;
                    if (collectionsViewModel != null)
                    {
                        collectionsViewModel.ActiveComponent = CollectionsViewModel.ActiveViews.MessagesView;
                    }
                });
            }
            else
            {
                Console.WriteLine($"Nenhuma mensagem nova: {previousCount} -> {newCount}. Mantendo na view atual.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar novas mensagens após criação de coleção: {ex.Message}");
        }
    }

}