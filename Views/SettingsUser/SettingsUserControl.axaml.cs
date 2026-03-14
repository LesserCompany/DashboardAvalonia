using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels.Shared;
using LesserDashboardClient.Views.Shared;

namespace LesserDashboardClient.Views.SettingsUser;

public partial class SettingsUserControl : UserControl
{
    private OpenInWebButton? _openInWebButton;

    public SettingsUserControl()
    {
        InitializeComponent();
        Loaded += SettingsUserControl_Loaded;
    }

    private void SettingsUserControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _openInWebButton = this.FindControl<OpenInWebButton>("OpenInWebButtonControl");

        if (_openInWebButton != null && DataContext is ViewModels.SettingsUser.SettingsUserViewModel viewModel)
        {
            var openInWebViewModel = _openInWebButton.DataContext as OpenInWebButtonViewModel;
            if (openInWebViewModel != null)
            {
                openInWebViewModel.WebUrl = viewModel.UrlWeb;
            }
        }
    }
}
