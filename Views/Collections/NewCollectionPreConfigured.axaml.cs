using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.ViewModels.Collections;
using MsBox.Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class NewCollectionPreConfigured : UserControl
{
    private TextBox? _tbCollectionNamePreConfigured;
    
    public NewCollectionPreConfigured()
    {
        InitializeComponent();
        this.Loaded += NewCollectionPreConfigured_Loaded;
    }

    private void NewCollectionPreConfigured_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _tbCollectionNamePreConfigured = this.FindControl<TextBox>("tbCollectionNamePreConfigured");
        
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
        if (_tbCollectionNamePreConfigured == null || DataContext is not CollectionsViewModel vm)
            return;

        if (vm.TbCollectionNameHasError)
        {
            if (!_tbCollectionNamePreConfigured.Classes.Contains("error"))
                _tbCollectionNamePreConfigured.Classes.Add("error");
        }
        else
        {
            _tbCollectionNamePreConfigured.Classes.Remove("error");
        }
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
    private void Border_OnDragOver(object? sender, DragEventArgs e)
    {
        // S� permite arquivos
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
                        .GetMessageBoxStandard("", "N�o foi poss�vel carregar o arquivo.");
                    await bbox.ShowWindowDialogAsync(MainWindow.instance);
                    return;
                }

                var firstFile = files.First();

                if (!string.Equals(Path.GetExtension(firstFile), ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    var bbox = MessageBoxManager
                        .GetMessageBoxStandard("", "Por favor, selecione um arquivo Excel v�lido (.xlsx).");
                    await bbox.ShowWindowDialogAsync(MainWindow.instance);
                    return;
                }


                if(string.IsNullOrEmpty(vm.TbRecFolder))
                    {
                    var bbox = MessageBoxManager
                        .GetMessageBoxStandard("", "A pasta de reconhecimentos especificada n�o existe.");
                    await bbox.ShowWindowDialogAsync(MainWindow.instance);
                    return;
                }

                var fileInfo = new FileInfo(firstFile);
                vm.UpdateGraduateDataFromFile(fileInfo);
            }
        }
    }
}