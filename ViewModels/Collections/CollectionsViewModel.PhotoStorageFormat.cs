using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedClientSide.ServerInteraction;

namespace LesserDashboardClient.ViewModels.Collections;

// Formato em que o Uploader grava as fotos da coleção (JPEG, padrão, ou WebP), para testar WebP em coleções
// escolhidas sem mudar as demais. Servidor em SetCollectionPhotoStorageFormat_v2. Dois pontos escondidos, ambos com
// Ctrl+Shift+Y:
//  - tela da coleção (CollectionView): painel para trocar o formato de uma coleção existente;
//  - formulários de criação/reenvio (NewCollection, NewCollectionPreConfigured): opção "Gravar fotos em WebP" nas
//    configurações avançadas, aplicada logo depois de a coleção ser gravada e antes de abrir o Uploader.
// Todo envio pelo Uploader lê o formato do servidor (PreparePhotoStorageFormatForUploadAsync): o formulário monta a
// coleção do zero e não traz o campo.
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

    // ---- Formulário de criação/reenvio ----

    [ObservableProperty] private bool isFormPhotoStorageFormatOptionVisible;
    [ObservableProperty] private bool formStoresPhotosAsWebP;

    public void ToggleFormPhotoStorageFormatOption()
    {
        IsFormPhotoStorageFormatOptionVisible = !IsFormPhotoStorageFormatOptionVisible;
        if (IsFormPhotoStorageFormatOptionVisible)
        {
            // Reenvio de coleção existente: começa no formato atual dela.
            var code = TbCollectionName?.Trim();
            var existing = string.IsNullOrEmpty(code) ? null
                : (SelectedCollection?.classCode == code ? SelectedCollection : CollectionsList.FirstOrDefault(c => c.classCode == code));
            FormStoresPhotosAsWebP = existing?.StoresPhotosAsWebP == true;
        }
    }

    private void ResetFormPhotoStorageFormatOption()
    {
        IsFormPhotoStorageFormatOptionVisible = false;
        FormStoresPhotosAsWebP = false;
    }

    // Grava no servidor o formato escolhido no formulário, só se a opção escondida foi aberta. false = falhou (já avisou).
    private async Task<bool> ApplyFormPhotoStorageFormatAsync(string classCode)
    {
        if (!IsFormPhotoStorageFormatOptionVisible)
            return true;

        var format = FormStoresPhotosAsWebP ? ProfessionalTask.PHOTO_STORAGE_FORMAT_WEBP : ProfessionalTask.PHOTO_STORAGE_FORMAT_JPEG;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.SetCollectionPhotoStorageFormat(classCode, format);
            if (r?.success == true)
                return true;
            GlobalAppStateViewModel.Instance.ShowDialogOk("Não foi possível gravar o formato das fotos (" + format + "): " + (r?.message ?? "sem resposta do servidor"));
        }
        catch (Exception e)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Não foi possível gravar o formato das fotos (" + format + "): " + e.Message);
        }
        return false;
    }

    // Antes de abrir o Uploader: aplica a escolha do formulário (se houver) e copia para pt o formato salvo no servidor,
    // que é o que o Uploader usa. false = não abrir o Uploader (a escolha WebP/JPEG não foi gravada).
    private async Task<bool> PreparePhotoStorageFormatForUploadAsync(ProfessionalTask pt)
    {
        try
        {
            if (!await ApplyFormPhotoStorageFormatAsync(pt.classCode))
                return false;

            try
            {
                var server = await GlobalAppStateViewModel.lfc.GetProfessionalTask(pt.classCode);
                pt.PhotoStorageFormat = server?.PhotoStorageFormat;
            }
            catch
            {
                // Sem resposta: vale a escolha do formulário, se houve; senão JPEG (padrão).
                pt.PhotoStorageFormat = IsFormPhotoStorageFormatOptionVisible && FormStoresPhotosAsWebP ? ProfessionalTask.PHOTO_STORAGE_FORMAT_WEBP : null;
            }

            // Mantém a lista em dia (o painel da coleção e o próximo envio leem dela).
            foreach (var c in CollectionsList.Where(c => c.classCode == pt.classCode && !ReferenceEquals(c, pt)))
                c.PhotoStorageFormat = pt.PhotoStorageFormat;
            return true;
        }
        finally
        {
            ResetFormPhotoStorageFormatOption();
        }
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
