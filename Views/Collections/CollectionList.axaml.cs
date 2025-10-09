using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using LesserDashboardClient.ViewModels.Collections;
using SharedClientSide.ServerInteraction.Users.Professionals;
using System.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class CollectionList : UserControl
{
    public CollectionList()
    {
        InitializeComponent();
        
        // Assinar evento de mudança de DataContext
        DataContextChanged += CollectionList_DataContextChanged;
        
        // Atualizar estilos quando o controle for carregado
        this.Loaded += (s, e) => 
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                UpdateActiveCardStyles();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        };
    }

    private void CollectionList_DataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is CollectionsViewModel vm)
        {
            // Assinar mudança de SelectedCollection
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == "SelectedCollection")
                {
                    // Usar Dispatcher para garantir que a UI está atualizada
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        UpdateActiveCardStyles();
                    }, Avalonia.Threading.DispatcherPriority.Background);
                }
                else if (args.PropertyName == "CollectionsListFiltered")
                {
                    // Atualizar quando a lista filtrada mudar
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        UpdateActiveCardStyles();
                    }, Avalonia.Threading.DispatcherPriority.Background);
                }
            };
            
            // Atualizar imediatamente se já houver uma seleção
            if (vm.GetType().GetProperty("SelectedCollection")?.GetValue(vm) != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateActiveCardStyles();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }
    }

    private void TextBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        if(DataContext is CollectionsViewModel vm)
        {
            vm.FilterProfessionalTasks(tbFilterClassCode.Text, tbFilterProfessional.Text);
        }
    }

    private void CardBorder_Tapped(object? sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.Tag != null)
        {
            if (DataContext is CollectionsViewModel vm)
            {
                // Usar reflexão para definir SelectedCollection
                var selectedCollectionProperty = vm.GetType().GetProperty("SelectedCollection");
                selectedCollectionProperty?.SetValue(vm, border.Tag);
                vm.ActiveComponent = CollectionsViewModel.ActiveViews.CollectionView;
                
                // Atualizar estilos dos cards
                UpdateActiveCardStyles();
            }
        }
    }

    private void UpdateActiveCardStyles()
    {
        if (DataContext is not CollectionsViewModel vm)
            return;

        // Encontrar o ItemsControl na árvore visual
        var itemsControl = this.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();
        if (itemsControl?.ItemsPanelRoot == null)
            return;

        // Obter a propriedade SelectedCollection usando reflexão
        var selectedCollectionProperty = vm.GetType().GetProperty("SelectedCollection");
        var selectedCollection = selectedCollectionProperty?.GetValue(vm);
        
        if (selectedCollection == null)
            return;

        // Percorrer todos os cards
        int updatedCount = 0;
        foreach (var child in itemsControl.ItemsPanelRoot.Children)
        {
            if (child is Border border && border.Name == "CardBorder")
            {
                var borderTag = border.Tag;
                var isSelected = ReferenceEquals(borderTag, selectedCollection);
                
                // Adicionar ou remover classe ActiveCard
                if (isSelected)
                {
                    if (!border.Classes.Contains("ActiveCard"))
                    {
                        border.Classes.Add("ActiveCard");
                        updatedCount++;
                    }
                }
                else
                {
                    if (border.Classes.Contains("ActiveCard"))
                    {
                        border.Classes.Remove("ActiveCard");
                    }
                }
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"UpdateActiveCardStyles: Updated {updatedCount} cards");
    }
}