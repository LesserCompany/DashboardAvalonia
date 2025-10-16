using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.Views;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.ProfessionalWindow
{
    public partial class ProfessionalWindowViewModel : ViewModelBase, IDisposable
    {
        private LesserFunctionClient lesserFunctionClient;

        [ObservableProperty] 
        private ObservableCollection<ProfessionalTask> contractsList = new();

        [ObservableProperty] 
        private ObservableCollection<ProfessionalTask> filteredContractsList = new();

        [ObservableProperty] 
        private ProfessionalTask? selectedContract;

        [ObservableProperty] 
        private string searchText = "";

        [ObservableProperty] 
        private bool isLoading = false;

        [ObservableProperty] 
        private bool isSelectButtonEnabled = false;

        [ObservableProperty] 
        private bool isSelectingContract = false;

        [ObservableProperty] 
        private int downloadProgress = 0;

        partial void OnSelectedContractChanged(ProfessionalTask? value)
        {
            IsSelectButtonEnabled = value != null;
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterContracts();
        }

        public ProfessionalWindowViewModel()
        {
            // Construtor sem parâmetros para o XAML
        }

        public ProfessionalWindowViewModel(LesserFunctionClient lfc)
        {
            lesserFunctionClient = lfc;
            LoadContractsFromServer();
        }

        private async void LoadContractsFromServer()
        {
            try
            {
                IsLoading = true;
                
                var professionalTasks = await lesserFunctionClient.getProfessionalTasks();
                if (professionalTasks != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ContractsList.Clear();
                        foreach (var task in professionalTasks)
                        {
                            ContractsList.Add(task);
                        }
                        FilterContracts();
                        
                        if (FilteredContractsList.Count > 0)
                        {
                            SelectedContract = FilteredContractsList.First();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error or show message
                System.Diagnostics.Debug.WriteLine($"Error loading contracts: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterContracts()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredContractsList.Clear();
                foreach (var contract in ContractsList)
                {
                    FilteredContractsList.Add(contract);
                }
            }
            else
            {
                var filtered = ContractsList.Where(x => 
                    x.classCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    x.companyUsername.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                FilteredContractsList.Clear();
                foreach (var contract in filtered)
                {
                    FilteredContractsList.Add(contract);
                }
            }

            // Update selection if current selection is not in filtered list
            if (SelectedContract != null && !FilteredContractsList.Contains(SelectedContract))
            {
                SelectedContract = FilteredContractsList.FirstOrDefault();
            }
        }

        [RelayCommand]
        public void SelectContractCommand()
        {
            if (SelectedContract == null) return;

            try
            {
                IsSelectingContract = true;
                DownloadProgress = 0;
                IsSelectButtonEnabled = false;

                // Start the download app for the selected contract
                App.StartDownloadApp(
                    SelectedContract, 
                    (progress) =>
                    {
                        // Update progress on UI thread
                        Dispatcher.UIThread.Post(() =>
                        {
                            DownloadProgress = progress;
                            System.Diagnostics.Debug.WriteLine($"Download progress: {progress}%");
                        });
                    },
                    () =>
                    {
                        // On done callback
                        Dispatcher.UIThread.Post(() =>
                        {
                            IsSelectingContract = false;
                            DownloadProgress = 100;
                            IsSelectButtonEnabled = true;
                            System.Diagnostics.Debug.WriteLine("Download completed successfully!");
                        });
                    },
                    (error) =>
                    {
                        // On error callback
                        Dispatcher.UIThread.Post(() =>
                        {
                            IsSelectingContract = false;
                            DownloadProgress = 0;
                            IsSelectButtonEnabled = true;
                            System.Diagnostics.Debug.WriteLine($"Download error: {error}");
                        });
                    }
                );
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    IsSelectingContract = false;
                    DownloadProgress = 0;
                    IsSelectButtonEnabled = true;
                });
                System.Diagnostics.Debug.WriteLine($"Error starting download app: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ExitCommand()
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var oldWindow = desktop.MainWindow;
                    var authWindow = new AuthWindow();
                    
                    desktop.MainWindow = authWindow;
                    authWindow.Show();
                    
                    // Espera um tick para garantir que a UI da nova janela iniciou
                    await Task.Delay(150);
                    
                    // Agora fecha a janela antiga (libera Dispatcher antigo)
                    (oldWindow?.DataContext as IDisposable)?.Dispose();
                    oldWindow?.Close();
                }
            });
        }

        [RelayCommand]
        public async Task LogoutCommand()
        {
            try
            {
                // Remove o arquivo de login para limpar as credenciais
                System.IO.File.Delete(LesserFunctionClient.loginFileInfo.FullName);
                
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var oldWindow = desktop.MainWindow;
                        
                        // Reaplica as configurações de tema e idioma antes de criar a nova janela de login
                        App.ReapplySettings();
                        
                        var authWindow = new AuthWindow();
                        
                        desktop.MainWindow = authWindow;
                        authWindow.Show();
                        
                        // Espera um tick para garantir que a UI da nova janela iniciou
                        await Task.Delay(150);
                        
                        // Agora fecha a janela antiga (libera Dispatcher antigo)
                        (oldWindow?.DataContext as IDisposable)?.Dispose();
                        oldWindow?.Close();
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

        public void Dispose()
        {
            // Limpa as coleções para liberar memória
            ContractsList?.Clear();
            FilteredContractsList?.Clear();
            
            // Cancela qualquer operação assíncrona pendente se necessário
            // (não temos CancellationToken neste caso, mas seria uma boa prática)
            
            // Limpa referências para evitar vazamentos de memória
            lesserFunctionClient = null;
            SelectedContract = null;
            
            // Força garbage collection se necessário (opcional)
            GC.Collect();
        }
    }
}
