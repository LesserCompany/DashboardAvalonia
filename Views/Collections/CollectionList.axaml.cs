using Avalonia.Controls;
using LesserDashboardClient.ViewModels.Collections;

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
}