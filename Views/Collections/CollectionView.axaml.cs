using Avalonia;
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
    }

    private void SeparationFileContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        // Na linha Nuvem não existe esse atalho: não abrir o menu.
        if (DataContext is CollectionsViewModel vm && vm.SelectedSeparationFile?.FileLocationType != ClassSeparationFile.FileLocationTypes.LOCAL)
            e.Cancel = true;
    }
}