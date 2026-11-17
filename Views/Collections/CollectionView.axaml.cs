using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.ComponentModel;
using LesserDashboardClient.ViewModels.Collections;
using SharedClientSide;

namespace LesserDashboardClient.Views.Collections;

public partial class CollectionView : UserControl
{
    private TopLevel? _topLevel;

    public CollectionView()
    {
        InitializeComponent();

        // Ctrl+Shift+Y abre/fecha o painel escondido do formato das fotos (JPEG/WebP) da coleção selecionada.
        // Escutado na janela (túnel): funciona com o foco em qualquer controle.
        AttachedToVisualTree += (_, _) =>
        {
            _topLevel = TopLevel.GetTopLevel(this);
            _topLevel?.AddHandler(KeyDownEvent, TopLevel_KeyDown, RoutingStrategies.Tunnel);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _topLevel?.RemoveHandler(KeyDownEvent, TopLevel_KeyDown);
            _topLevel = null;
        };

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

    private void TopLevel_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEffectivelyVisible || DataContext is not CollectionsViewModel vm)
            return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && (e.Key == Key.Y || e.PhysicalKey == PhysicalKey.Y))
        {
            vm.TogglePhotoStorageFormatPanel();
            e.Handled = true;
        }
    }

    private void SeparationFileContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        // Na linha Nuvem não existe esse atalho: não abrir o menu.
        if (DataContext is CollectionsViewModel vm && vm.SelectedSeparationFile?.FileLocationType != ClassSeparationFile.FileLocationTypes.LOCAL)
            e.Cancel = true;
    }
}