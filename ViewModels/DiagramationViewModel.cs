using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.Views;
using SharedClientSide.Helpers;
using System;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels;

public partial class DiagramationViewModel : ViewModelBase
{
    [ObservableProperty] private bool isLoading = false;
    [ObservableProperty] private int loadingProgress = 0;
    
    public bool CanStartDiagramation => !IsLoading;

    [RelayCommand]
    public async Task StartDiagramation()
    {
        try
        {
            IsLoading = true;
            LoadingProgress = 0;
            OnPropertyChanged(nameof(CanStartDiagramation));

            // Simular progresso inicial
            await Task.Delay(100);
            LoadingProgress = 20;

            // Iniciar a aplicação de diagramação
            await App.StartDiagramationWPFApp(UpdateProgress);

            LoadingProgress = 100;
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            // Em caso de erro, mostrar mensagem
            if (MainWindow.instance != null)
            {
                var errorBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                    "Erro", 
                    $"Erro ao iniciar diagramação: {ex.Message}",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await errorBox.ShowWindowDialogAsync(MainWindow.instance);
            }
        }
        finally
        {
            IsLoading = false;
            LoadingProgress = 0;
            OnPropertyChanged(nameof(CanStartDiagramation));
        }
    }

    private void UpdateProgress(int progress)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            LoadingProgress = Math.Max(20, Math.Min(90, progress));
        });
    }
}
