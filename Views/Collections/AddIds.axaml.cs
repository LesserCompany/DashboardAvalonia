using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LesserDashboardClient.ViewModels.Collections;
using MsBox.Avalonia;
using SharedClientSide.ServerInteraction.Users.Graduate;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class AddIds : UserControl
{
    private static readonly IBrush SuccessBackground = new SolidColorBrush(Color.FromArgb(30, 0, 180, 0));
    private static readonly IBrush FailedBackground = new SolidColorBrush(Color.FromArgb(30, 220, 0, 0));

    public AddIds()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private AddIdsViewModel? _currentVm;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
            _currentVm.PropertyChanged -= OnVmPropertyChanged;

        _currentVm = DataContext as AddIdsViewModel;

        if (_currentVm != null)
            _currentVm.PropertyChanged += OnVmPropertyChanged;

        HideFailedItems();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddIdsViewModel.IsRegistering) && _currentVm != null && !_currentVm.IsRegistering)
        {
            RefreshRowStyles();
            ShowFailedItemsSummary();
        }
    }

    private void GraduatesDataGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (_currentVm == null || _currentVm.ItemStatuses.Count == 0)
        {
            e.Row.Background = Brushes.Transparent;
            return;
        }

        if (e.Row.DataContext is GraduateByCPFWithPhotos grad && !string.IsNullOrWhiteSpace(grad.ShortPath))
        {
            var status = _currentVm.GetItemStatus(grad.ShortPath);
            e.Row.Background = status.Status switch
            {
                AddIdRegistrationStatus.Success => SuccessBackground,
                AddIdRegistrationStatus.Failed => FailedBackground,
                _ => Brushes.Transparent
            };
        }
        else
        {
            e.Row.Background = Brushes.Transparent;
        }
    }

    private void RefreshRowStyles()
    {
        var items = GraduatesDataGrid.ItemsSource;
        GraduatesDataGrid.ItemsSource = null;
        GraduatesDataGrid.ItemsSource = items;
    }

    private void ShowFailedItemsSummary()
    {
        if (_currentVm == null)
            return;

        var failedItems = _currentVm.ItemStatuses
            .Where(kvp => kvp.Value.Status == AddIdRegistrationStatus.Failed)
            .Select(kvp => $"{kvp.Key}: {kvp.Value.FailureReason}")
            .ToList();

        if (failedItems.Count > 0)
        {
            FailedItemsHeader.Text = $"{failedItems.Count} item(ns) falharam:";
            FailedItemsList.ItemsSource = new ObservableCollection<string>(failedItems);
            FailedItemsExpander.IsVisible = true;
            FailedItemsExpander.IsExpanded = true;
        }
        else
        {
            HideFailedItems();
        }
    }

    private void HideFailedItems()
    {
        FailedItemsExpander.IsVisible = false;
        FailedItemsExpander.IsExpanded = false;
        FailedItemsList.ItemsSource = null;
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

        HideFailedItems();

        IStorageFolder? startFolder = null;
        try
        {
            startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(vm.TbRecFolder);
        }
        catch
        {
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

