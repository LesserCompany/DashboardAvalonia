using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels.Shared;
using LesserDashboardClient.Views.Shared;

namespace LesserDashboardClient.Views.Invoices;

public partial class InvoicesControl : UserControl
{
    private OpenInWebButton? _openInWebButton;

    public InvoicesControl()
    {
        InitializeComponent();
        this.Loaded += InvoicesControl_Loaded;
    }

    private void InvoicesControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _openInWebButton = this.FindControl<OpenInWebButton>("OpenInWebButtonControl");

        if (_openInWebButton != null && DataContext is ViewModels.Invoices.InvoicesViewModel viewModel)
        {
            var openInWebViewModel = _openInWebButton.DataContext as OpenInWebButtonViewModel;
            if (openInWebViewModel != null)
            {
                openInWebViewModel.WebUrl = viewModel.UrlWeb;
            }
        }
    }
}