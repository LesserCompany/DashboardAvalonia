using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedClientSide.ServerInteraction;

namespace LesserDashboardClient.ViewModels.Collections;

// Formato em que o Uploader grava as fotos da coleção (JPEG, padrão, ou WebP), para testar WebP em coleções
// escolhidas sem mudar as demais. Painel escondido: só aparece com Ctrl+Shift+Y na tela da coleção (ver
// CollectionView.axaml.cs). Servidor em SetCollectionPhotoStorageFormat_v2.
public partial class CollectionsViewModel
{
    [ObservableProperty] private bool isPhotoStorageFormatPanelVisible;
    [ObservableProperty] private bool photoStorageFormatWebPSelected;
    [ObservableProperty] private bool isSavingPhotoStorageFormat;
    [ObservableProperty] private string? photoStorageFormatMessage;
    [ObservableProperty] private bool photoStorageFormatMessageIsError;

    public string SelectedCollectionPhotoStorageFormatText =>
        SelectedCollection?.StoresPhotosAsWebP == true ? "WebP" : "JPEG";

    public void TogglePhotoStorageFormatPanel()
    {
        if (SelectedCollection == null)
            return;
        IsPhotoStorageFormatPanelVisible = !IsPhotoStorageFormatPanelVisible;
        if (IsPhotoStorageFormatPanelVisible)
            ResetPhotoStorageFormatPanel();
    }

    // Ao trocar de coleção com o painel aberto, mostra o formato da nova.
    private void ResetPhotoStorageFormatPanel()
    {
        PhotoStorageFormatWebPSelected = SelectedCollection?.StoresPhotosAsWebP == true;
        PhotoStorageFormatMessage = null;
        PhotoStorageFormatMessageIsError = false;
        OnPropertyChanged(nameof(SelectedCollectionPhotoStorageFormatText));
    }

    partial void OnSelectedCollectionChanged(ProfessionalTask? oldValue, ProfessionalTask newValue)
    {
        if (IsPhotoStorageFormatPanelVisible)
            ResetPhotoStorageFormatPanel();
        else
            OnPropertyChanged(nameof(SelectedCollectionPhotoStorageFormatText));
    }

    [RelayCommand]
    private void ClosePhotoStorageFormatPanel()
    {
        IsPhotoStorageFormatPanelVisible = false;
    }

    [RelayCommand]
    private async Task SavePhotoStorageFormat()
    {
        var collection = SelectedCollection;
        if (collection == null || IsSavingPhotoStorageFormat)
            return;

        var format = PhotoStorageFormatWebPSelected ? ProfessionalTask.PHOTO_STORAGE_FORMAT_WEBP : ProfessionalTask.PHOTO_STORAGE_FORMAT_JPEG;
        IsSavingPhotoStorageFormat = true;
        PhotoStorageFormatMessage = null;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.SetCollectionPhotoStorageFormat(collection.classCode, format);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (r?.success == true)
                {
                    // Mesma instância que vai para o Uploader (App.StartUploadConcurrentApp serializa este objeto).
                    collection.PhotoStorageFormat = r.Content?.PhotoStorageFormat;
                    if (SelectedCollection == collection)
                    {
                        PhotoStorageFormatWebPSelected = collection.StoresPhotosAsWebP;
                        PhotoStorageFormatMessage = "Salvo. Vale para as próximas fotos enviadas pelo Uploader; as já enviadas ficam como estão.";
                        PhotoStorageFormatMessageIsError = false;
                        OnPropertyChanged(nameof(SelectedCollectionPhotoStorageFormatText));
                    }
                }
                else if (SelectedCollection == collection)
                {
                    PhotoStorageFormatMessage = string.IsNullOrWhiteSpace(r?.message) ? "Não foi possível salvar o formato." : r.message;
                    PhotoStorageFormatMessageIsError = true;
                }
            });
        }
        catch (Exception e)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (SelectedCollection == collection)
                {
                    PhotoStorageFormatMessage = "Não foi possível salvar o formato. " + e.Message;
                    PhotoStorageFormatMessageIsError = true;
                }
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsSavingPhotoStorageFormat = false);
        }
    }
}
