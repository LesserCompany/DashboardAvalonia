using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

    public MainWindowViewModel()
    {
        Instance = this;
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
        File.Delete(LesserFunctionClient.loginFileInfo.FullName);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if(desktop.MainWindow != null)
                    desktop.MainWindow.Close();
            }
            App.StartAuthWindow();
        });
    }

}