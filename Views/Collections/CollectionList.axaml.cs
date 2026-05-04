using Avalonia.Controls;
using LesserDashboardClient.ViewModels.Collections;
using SharedClientSide.ServerInteraction;

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

    private void MoreActionsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ProfessionalTask item && DataContext is CollectionsViewModel vm)
        {
            vm.PendingItemForCancelDeletion = item;
        }
    }
}