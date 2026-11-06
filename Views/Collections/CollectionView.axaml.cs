using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.ComponentModel;
using LesserDashboardClient.ViewModels.Collections;
using SharedClientSide;

namespace LesserDashboardClient.Views.Collections;

public partial class CollectionView : UserControl
{
    public CollectionView()
    {
        InitializeComponent();

        // Ao abrir a edição da label (lápis do topo ou "Renomear coleção" em Avançado, que fica lá embaixo),
        // rola até o campo e põe o cursor nele.
        LabelEditPanel.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty || !LabelEditPanel.IsVisible)
                return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LabelEditPanel.BringIntoView();
                LabelEditBox.Focus();
                LabelEditBox.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Background);
        };
    }

    private void SeparationFileContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        // Na linha Nuvem não existe esse atalho: não abrir o menu.
        if (DataContext is CollectionsViewModel vm && vm.SelectedSeparationFile?.FileLocationType != ClassSeparationFile.FileLocationTypes.LOCAL)
            e.Cancel = true;
    }
}