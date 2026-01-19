using Avalonia.Controls;
using LesserDashboardClient.ViewModels;

namespace LesserDashboardClient.Views.Collections;

/// <summary>
/// Página de boas-vindas do Dashboard
/// </summary>
public partial class WelcomeView : UserControl
{
    public WelcomeView()
    {
        InitializeComponent();
        Loaded += WelcomeView_Loaded;
    }

    private void WelcomeView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Carrega o nome da empresa do GlobalAppStateViewModel
        try
        {
            if (GlobalAppStateViewModel.lfc?.loginResult?.User?.company != null)
            {
                CompanyNameText.Text = GlobalAppStateViewModel.lfc.loginResult.User.company;
            }
            else
            {
                CompanyNameText.Text = "";
            }
        }
        catch
        {
            CompanyNameText.Text = "";
        }
    }
}

