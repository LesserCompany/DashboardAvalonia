using Avalonia.Controls;
using SharedClientSide_AVALONIA.Helpers;

namespace LesserDashboardClient.Views;

public partial class MainWindow : Window
{
    public static MainWindow instance;
    public MainWindow()
    {
        InitializeComponent();
        instance = this;
        
        // Registra os comandos de teclado compartilhados (Ctrl+Shift+L e Ctrl+Shift+K)
        EndpointConfigHelper.RegisterKeyboardShortcuts(this, "Dashboard");
    }
}