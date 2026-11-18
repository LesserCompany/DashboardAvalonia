using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LesserDashboardClient.ViewModels.Collections;
using LesserDashboardClient.Views;
using MsBox.Avalonia;
using SharedClientSide.ServerInteraction.Users.Graduate;
using System;
using System.Collections.Generic;
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

    private async void DownloadParticipantsExcel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EditIdsViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return;

        var safeName = string.Concat((vm.TbCollectionName ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Excel dos participantes",
            SuggestedFileName = $"participantes-{(safeName == "" ? "colecao" : safeName)}-{DateTime.Now:yyyy-MM-dd}.xlsx",
            DefaultExtension = "xlsx",
            ShowOverwritePrompt = true,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Excel") { Patterns = new List<string> { "*.xlsx" } }
            }
        });
        if (file == null)
            return;

        DownloadParticipantsExcelButton.IsEnabled = false;
        string? error;
        try
        {
            // Gera em memória e só grava se deu certo (erro não deixa arquivo vazio/pela metade).
            using var buffer = new MemoryStream();
            error = await vm.ExportParticipantsExcelAsync(buffer);
            if (error == null)
            {
                await using var stream = await file.OpenWriteAsync();
                stream.SetLength(0);
                buffer.Position = 0;
                await buffer.CopyToAsync(stream);
            }
        }
        catch (IOException ex)
        {
            error = "Não foi possível salvar o arquivo. Se ele estiver aberto no Excel, feche e tente novamente.\n\n" + ex.Message;
        }
        catch (Exception ex)
        {
            error = "Erro ao gerar o Excel: " + ex.Message;
        }
        finally
        {
            DownloadParticipantsExcelButton.IsEnabled = true;
        }

        var path = file.TryGetLocalPath() ?? file.Name;
        var box = error == null
            ? MessageBoxManager.GetMessageBoxStandard("", $"Excel salvo em:\n{path}\n\nEdite os dados e arraste o arquivo para a área abaixo. Não altere a coluna \"{EditIdsViewModel.ParticipantIdHeader}\".")
            : MessageBoxManager.GetMessageBoxStandard("Erro", error);
        await box.ShowWindowDialogAsync(MainWindow.instance);
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
