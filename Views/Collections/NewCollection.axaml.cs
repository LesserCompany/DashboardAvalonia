using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.ViewModels.Collections;
using LesserDashboardClient.Views;
using MsBox.Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class NewCollection : UserControl
{
    private TextBox? _tbCollectionName;
    
    public NewCollection()
    {
        InitializeComponent();
        this.Loaded += NewCollection_Loaded;
    }

    private void NewCollection_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _tbCollectionName = this.FindControl<TextBox>("tbCollectionName");
        
        if (DataContext is CollectionsViewModel vm)
        {
            vm.PropertyChanged += Vm_PropertyChanged;
        }
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CollectionsViewModel.TbCollectionNameHasError))
        {
            UpdateTextBoxErrorState();
        }
    }

    private void UpdateTextBoxErrorState()
    {
        if (_tbCollectionName == null || DataContext is not CollectionsViewModel vm)
            return;

        if (vm.TbCollectionNameHasError)
        {
            if (!_tbCollectionName.Classes.Contains("error"))
                _tbCollectionName.Classes.Add("error");
        }
        else
        {
            _tbCollectionName.Classes.Remove("error");
        }
    }

    private void TextBox_TextInput(object? sender, Avalonia.Input.TextInputEventArgs e)
    {
        if(DataContext is CollectionsViewModel vm)
        {
            if(string.IsNullOrEmpty(e.Text))
                return;
            if (!vm.IsTextAllowed(e.Text))
            {
                e.Handled = true;
            }
        }
    }
    private void Border_OnDragOver(object? sender, DragEventArgs e)
    {
        // Só permite arquivos
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void Border_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is CollectionsViewModel vm)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFileNames()?.ToList();

                if (files == null || files.Count == 0)
                {
                    var bbox = MessageBoxManager
                        .GetMessageBoxStandard("", "Não foi possível carregar o arquivo.");
                    await bbox.ShowWindowDialogAsync(MainWindow.instance);
                    return;
                }

                var firstFile = files.First();

                if (!string.Equals(Path.GetExtension(firstFile), ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    var bbox = MessageBoxManager
                        .GetMessageBoxStandard("", "Por favor, selecione um arquivo Excel válido (.xlsx).");
                    await bbox.ShowWindowDialogAsync(MainWindow.instance);
                    return;
                }


                if (string.IsNullOrEmpty(vm.TbRecFolder))
                {
                    var bbox = MessageBoxManager
                        .GetMessageBoxStandard("", "A pasta de reconhecimentos especificada não existe.");
                    await bbox.ShowWindowDialogAsync(MainWindow.instance);
                    return;
                }

                var fileInfo = new FileInfo(firstFile);
                vm.UpdateGraduateDataFromFile(fileInfo);
            }
        }
    }

    private async void OpenEventFolderDialog_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Selecionar pasta de eventos"
        });

        var folder = folders.FirstOrDefault();
        if (folder != null && DataContext is CollectionsViewModel vm)
        {
            string selectedPath = folder.Path.LocalPath;
            
            // Validar se a pasta é de eventos
            if (IsValidEventFolder(selectedPath))
            {
                vm.TbEventFolder = selectedPath;
            }
            else
            {
                var bbox = MessageBoxManager.GetMessageBoxStandard(
                    "Pasta inválida",
                    "A pasta selecionada não é uma pasta de eventos válida. Por favor, selecione uma pasta que contenha '1.eventos', 'eventos' ou 'event' no nome.",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await bbox.ShowWindowDialogAsync(MainWindow.instance);
            }
        }
    }

    private async void OpenRecFolderDialog_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Selecionar pasta de reconhecimentos"
        });

        var folder = folders.FirstOrDefault();
        if (folder != null && DataContext is CollectionsViewModel vm)
        {
            string selectedPath = folder.Path.LocalPath;
            
            // Validar se a pasta é de reconhecimentos
            if (IsValidRecognitionFolder(selectedPath))
            {
                vm.TbRecFolder = selectedPath;
            }
            else
            {
                var bbox = MessageBoxManager.GetMessageBoxStandard(
                    "Pasta inválida",
                    "A pasta selecionada não é uma pasta de reconhecimentos válida. Por favor, selecione uma pasta que contenha '2.reconhecimentos', 'reconhecimentos', 'reco' ou 'id' no nome.",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await bbox.ShowWindowDialogAsync(MainWindow.instance);
            }
        }
    }

    private bool IsValidEventFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return false;

        DirectoryInfo dir = new DirectoryInfo(folderPath);
        string dirName = dir.Name.ToLower();
        
        // Verificar se o nome da pasta contém indicadores de eventos
        if (dirName == "1.eventos" || 
            dirName.Contains("1.eventos_grande") || 
            dirName.Contains("event"))
        {
            return true;
        }

        // Verificar subpastas
        var subDirectories = dir.GetDirectories().ToList();
        if (subDirectories.Any(d => 
            d.Name.ToLower() == "1.eventos" || 
            d.Name.ToLower().Contains("1.eventos_grande") || 
            d.Name.ToLower().Contains("event")))
        {
            return true;
        }

        return false;
    }

    private bool IsValidRecognitionFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return false;

        DirectoryInfo dir = new DirectoryInfo(folderPath);
        string dirName = dir.Name.ToLower();
        
        // Verificar se o nome da pasta contém indicadores de reconhecimentos
        if (dirName == "2.reconhecimentos" || 
            dirName.Contains("2.reconhecimentos_grande") || 
            dirName.Contains("reco") || 
            dirName.Contains("id") ||
            dirName.Contains("2.id"))
        {
            return true;
        }

        // Verificar subpastas
        var subDirectories = dir.GetDirectories().ToList();
        if (subDirectories.Any(d => 
            d.Name.ToLower() == "2.reconhecimentos" || 
            d.Name.ToLower().Contains("2.reconhecimentos_grande") || 
            d.Name.ToLower().Contains("reco") ||
            d.Name.ToLower().Contains("id") ||
            d.Name.ToLower().Contains("2.id")))
        {
            return true;
        }

        return false;
    }
}