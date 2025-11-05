using Avalonia.Controls;
using Avalonia.Input;
using System.ComponentModel;

namespace LesserDashboardClient.Views;

public partial class MainWindow : Window
{
    public static MainWindow instance;
    public MainWindow()
    {
        InitializeComponent();
        instance = this;
        KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        // Detecta Ctrl+Shift+K
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.K)
        {
            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.ShowEndpointsInfoCommandCommand?.Execute(null);
            }
        }
    }
}