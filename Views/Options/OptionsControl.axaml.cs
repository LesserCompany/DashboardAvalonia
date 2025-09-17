using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CodingSeb.Localization.Avalonia;
using LesserDashboardClient.ViewModels.Options;
using System.Linq;
//using Avalonia.Platform.Storage;

namespace LesserDashboardClient.Views.Options;

public partial class OptionsControl : UserControl
{
    public OptionsControl()
    {
        InitializeComponent();
    }
    private async void OpenFolderDialog_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = ""
        });

        var folder = folders.FirstOrDefault();
        if (folder != null && DataContext is OptionsControlViewModel vm)
        {
            vm.ChangePathToDownloadApp(folder.Path.LocalPath);
        }
    }
}