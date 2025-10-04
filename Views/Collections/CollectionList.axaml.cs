using Avalonia.Controls;
using Avalonia.Interactivity;
using LesserDashboardClient.ViewModels.Collections;
using SharedClientSide.ServerInteraction.Users.Professionals;

namespace LesserDashboardClient.Views.Collections;

public partial class CollectionList : UserControl
{
    public CollectionList()
    {
        InitializeComponent();
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
            }
        }
    }
}