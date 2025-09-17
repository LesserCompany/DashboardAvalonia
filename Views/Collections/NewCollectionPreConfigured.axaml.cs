using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels.Collections;

namespace LesserDashboardClient.Views.Collections;

public partial class NewCollectionPreConfigured : UserControl
{
    public NewCollectionPreConfigured()
    {
        InitializeComponent();
    }
    private void TextBox_TextInput(object? sender, Avalonia.Input.TextInputEventArgs e)
    {
        if (DataContext is CollectionsViewModel vm)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;
            if (!vm.IsTextAllowed(e.Text))
            {
                e.Handled = true;
            }
        }
    }
}