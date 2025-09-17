using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LesserDashboardClient.Views;

public partial class AuthWindow : Window
{
    public AuthWindow()
    {
        InitializeComponent();
        KeyDown += AuthWindow_KeyDown; ;
    }

    private void AuthWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if(e.Key == Avalonia.Input.Key.Enter)
        {
            if(DataContext is ViewModels.Auth.AuthViewModel vm)
            {
                vm.LoginCommandCommand.Execute(null);
            }
        }
    }
}