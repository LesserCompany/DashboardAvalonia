using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels.Shared;
using LesserDashboardClient.Views.Shared;

namespace LesserDashboardClient.Views.Personalize;

public partial class PersonalizeControl : UserControl
{
    private OpenInWebButton? _openInWebButton;

    public PersonalizeControl()
    {
        InitializeComponent();
        Loaded += PersonalizeControl_Loaded;
    }

    private void PersonalizeControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _openInWebButton = this.FindControl<OpenInWebButton>("OpenInWebButtonControl");

        if (_openInWebButton != null && DataContext is ViewModels.Personalize.PersonalizeViewModel viewModel)
        {
            var openInWebViewModel = _openInWebButton.DataContext as OpenInWebButtonViewModel;
            if (openInWebViewModel != null)
            {
                openInWebViewModel.WebUrl = viewModel.UrlWeb;
            }
        }
    }
}
