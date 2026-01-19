using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels.Shared;
using LesserDashboardClient.Views.Shared;

namespace LesserDashboardClient.Views.SearchGraduate;

public partial class SearchGraduateControl : UserControl
{
    private TabControl? _tabControl;
    private OpenInWebButton? _openInWebButton;

    public SearchGraduateControl()
    {
        InitializeComponent();
        this.Loaded += SearchGraduateControl_Loaded;
    }

    private void SearchGraduateControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _tabControl = this.FindControl<TabControl>("PART_TabControl");
        _openInWebButton = this.FindControl<OpenInWebButton>("OpenInWebButtonControl");

        if (_tabControl != null && _openInWebButton != null && DataContext is ViewModels.SearchGraduate.SearchGraduateViewModel viewModel)
        {
            _tabControl.SelectionChanged += (s, args) =>
            {
                UpdateOpenInWebButtonUrl(viewModel);
            };

            // Inicializar com a primeira aba
            UpdateOpenInWebButtonUrl(viewModel);
        }
    }

    private void UpdateOpenInWebButtonUrl(ViewModels.SearchGraduate.SearchGraduateViewModel viewModel)
    {
        if (_tabControl == null || _openInWebButton == null)
            return;

        var selectedIndex = _tabControl.SelectedIndex;
        var openInWebViewModel = _openInWebButton.DataContext as OpenInWebButtonViewModel;

        if (openInWebViewModel != null)
        {
            switch (selectedIndex)
            {
                case 0: // Busca de CPF
                    openInWebViewModel.WebUrl = viewModel.UrlSearchCPFWeb;
                    break;
                case 1: // Revisão de fotos escolhidas pelos CPFs
                    openInWebViewModel.WebUrl = viewModel.UrlReviewPhotosWeb;
                    break;
                case 2: // Fotos para tratamento manual
                    openInWebViewModel.WebUrl = viewModel.UrlPhotosForTreatmentWeb;
                    break;
                case 3: // Personalizar
                    openInWebViewModel.WebUrl = viewModel.UrlPersonalizeWeb;
                    break;
                default:
                    openInWebViewModel.WebUrl = "";
                    break;
            }
        }
    }
}