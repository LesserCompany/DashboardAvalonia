using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using LesserDashboardClient.ViewModels.Collections;
using SharedClientSide.ServerInteraction.Users.Professionals;

namespace LesserDashboardClient.Views.Collections;

public partial class SelectProfessional : UserControl
{
    public SelectProfessional()
    {
        InitializeComponent();
    }

    private void ProfessionalCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: Professional professional })
            return;

        var vm = CollectionsViewModel.Instance;
        if (vm == null)
            return;

        vm.SelectedProfessional = professional;
        e.Handled = true;
    }

    private void DeleteProfessionalButton_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Impede o clique de "subir" e selecionar o card.
        e.Handled = true;
    }

    private async void DeleteProfessionalButton_OnClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button { Tag: Professional professional })
            return;

        var vm = CollectionsViewModel.Instance;
        if (vm == null || vm.RemoveProfessionalIsRunning)
            return;

        await vm.RemoveProfessionalAsync(professional);
    }
}
