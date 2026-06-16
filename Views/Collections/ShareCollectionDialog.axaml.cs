using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LesserDashboardClient.Views.Collections;

public partial class ShareCollectionDialog : Window
{
    public ShareCollectionDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
