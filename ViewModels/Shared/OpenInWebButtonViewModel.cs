using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.ViewModels;
using System;
using System.Diagnostics;

namespace LesserDashboardClient.ViewModels.Shared
{
    public partial class OpenInWebButtonViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _webUrl = "";

        [RelayCommand]
        public void OpenInWeb()
        {
            if (string.IsNullOrEmpty(WebUrl))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = WebUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao abrir link: {ex.Message}");
            }
        }
    }
}
