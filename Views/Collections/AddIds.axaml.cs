using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LesserDashboardClient.ViewModels.Collections;
using MsBox.Avalonia;
using SharedClientSide.ServerInteraction.Users.Graduate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class AddIds : UserControl
{
    public AddIds()
    {
        InitializeComponent();
    }

    private async void NewId_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return;

        if (DataContext is not AddIdsViewModel vm)
            return;

        if (string.IsNullOrWhiteSpace(vm.TbRecFolder) || !Directory.Exists(vm.TbRecFolder))
        {
            var bbox = MessageBoxManager.GetMessageBoxStandard(
                "Pasta inválida",
                "Não foi possível localizar a pasta de reconhecimentos desta coleção neste computador.",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error);
            await bbox.ShowWindowDialogAsync(MainWindow.instance);
            return;
        }

        IStorageFolder? startFolder = null;
        try
        {
            startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(vm.TbRecFolder);
        }
        catch
        {
            // Ignorar: abre o picker sem sugestão.
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Selecionar novo(s) ID(s)",
            SuggestedStartLocation = startFolder,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Imagens")
                {
                    Patterns = new List<string> { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp" }
                },
                FilePickerFileTypes.All
            }
        });

        if (files == null || files.Count == 0)
            return;

        var recRoot = vm.TbRecFolder.TrimEnd('\\', '/');
        int added = 0;

        foreach (var f in files)
        {
            var fullPath = f?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                continue;

            string shortPath;
            if (fullPath.StartsWith(recRoot, StringComparison.OrdinalIgnoreCase))
                shortPath = fullPath.Substring(recRoot.Length).TrimStart('\\', '/');
            else
                shortPath = Path.GetFileName(fullPath);

            if (string.IsNullOrWhiteSpace(shortPath))
                continue;

            if (vm.GraduatesData.Any(g => g != null && string.Equals(g.ShortPath, shortPath, StringComparison.OrdinalIgnoreCase)))
                continue;

            vm.GraduatesData.Add(new GraduateByCPFWithPhotos
            {
                ShortPath = shortPath,
                Name = "",
                Blocked = false,
                BlockType = GraduateByCPF.BlockTypes.WATERMARK
            });
            added++;
        }

        if (added > 0)
            vm.SortGraduatesDataAlphabetically();
    }
}

