using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LesserDashboardClient.ViewModels.Collections;
using LesserDashboardClient.Views;
using MsBox.Avalonia;
using SharedClientSide.ServerInteraction.Users.Graduate;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class EditIds : UserControl
{
    private static readonly IBrush SuccessBackground = new SolidColorBrush(Color.FromArgb(30, 0, 180, 0));
    private static readonly IBrush FailedBackground = new SolidColorBrush(Color.FromArgb(30, 220, 0, 0));

    public EditIds()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private EditIdsViewModel? _currentVm;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
            _currentVm.PropertyChanged -= OnVmPropertyChanged;

        _currentVm = DataContext as EditIdsViewModel;

        if (_currentVm != null)
            _currentVm.PropertyChanged += OnVmPropertyChanged;

        HideFailedItems();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_currentVm == null)
            return;

        if (e.PropertyName == nameof(EditIdsViewModel.IsRegistering) && !_currentVm.IsRegistering)
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
                EditIdRegistrationStatus.Success => SuccessBackground,
                EditIdRegistrationStatus.Failed => FailedBackground,
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
            .Where(kvp => kvp.Value.Status == EditIdRegistrationStatus.Failed)
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

    private void ExcelDropZone_OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void ExcelDropZone_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not EditIdsViewModel vm)
            return;

        if (!e.Data.Contains(DataFormats.Files))
            return;

        var files = e.Data.GetFileNames()?.ToList();
        if (files == null || files.Count == 0)
        {
            var bbox = MessageBoxManager.GetMessageBoxStandard("", "Não foi possível carregar o arquivo.");
            await bbox.ShowWindowDialogAsync(MainWindow.instance);
            return;
        }

        var firstFile = files.First();
        if (!string.Equals(Path.GetExtension(firstFile), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var bbox = MessageBoxManager.GetMessageBoxStandard("", "Por favor, selecione um arquivo Excel válido (.xlsx).");
            await bbox.ShowWindowDialogAsync(MainWindow.instance);
            return;
        }

        if (string.IsNullOrWhiteSpace(vm.RecFolderForExcel) || !Directory.Exists(vm.RecFolderForExcel))
        {
            var bbox = MessageBoxManager.GetMessageBoxStandard("", "A pasta de reconhecimentos especificada não existe.");
            await bbox.ShowWindowDialogAsync(MainWindow.instance);
            return;
        }

        HideFailedItems();

        var importError = vm.ImportGraduateDataFromExcelFile(new FileInfo(firstFile));
        if (importError != null)
        {
            var bbox = MessageBoxManager.GetMessageBoxStandard("Erro", importError);
            await bbox.ShowWindowDialogAsync(MainWindow.instance);
        }
    }
}
