using Avalonia.Controls;
using System.ComponentModel;

namespace LesserDashboardClient.Views;

public partial class MainWindow : Window
{
    public static MainWindow instance;
    public MainWindow()
    {
        InitializeComponent();
        instance = this;
    }
}