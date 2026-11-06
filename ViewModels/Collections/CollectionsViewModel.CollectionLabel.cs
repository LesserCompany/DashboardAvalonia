using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LesserDashboardClient.ViewModels.Collections;

// Label da coleção: nome de exibição que o usuário troca quando quiser (o classCode não muda, o bucket depende
// dele). Editada no cabeçalho da CollectionView; servidor em SetCollectionLabel_v2, que recusa nome repetido.
public partial class CollectionsViewModel
{
    // Mesmo limite de PgService.CollectionLabelMaxLength no servidor.
    public const int CollectionLabelMaxLength = 100;

    [ObservableProperty] private bool isEditingLabel;
    [ObservableProperty] private string editLabelText = string.Empty;
    [ObservableProperty] private string? labelEditError;
    [ObservableProperty] private bool isSavingLabel;

    // classCode da coleção em edição: a resposta do servidor pode chegar depois de o usuário trocar de coleção.
    private string? _labelEditClassCode;

    [RelayCommand]
    private void StartEditLabel()
    {
        if (SelectedCollection == null)
            return;
        _labelEditClassCode = SelectedCollection.classCode;
        EditLabelText = SelectedCollection.Label ?? string.Empty;
        LabelEditError = null;
        IsEditingLabel = true;
    }

    [RelayCommand]
    private void CancelEditLabel()
    {
        IsEditingLabel = false;
        LabelEditError = null;
        _labelEditClassCode = null;
    }

    [RelayCommand]
    private async Task SaveLabel()
    {
        var collection = SelectedCollection;
        if (collection == null || IsSavingLabel || collection.classCode != _labelEditClassCode)
            return;

        var label = (EditLabelText ?? string.Empty).Trim();
        if (label.Length > CollectionLabelMaxLength)
        {
            LabelEditError = string.Format(Loc.Tr("The name can have at most {0} characters."), CollectionLabelMaxLength);
            return;
        }
        if (string.Equals(label, collection.Label ?? string.Empty, StringComparison.Ordinal))
        {
            CancelEditLabel();
            return;
        }

        IsSavingLabel = true;
        LabelEditError = null;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.SetCollectionLabel(collection.classCode, label);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (r?.success == true)
                {
                    // Mesma instância das listas: a linha da lista lateral também atualiza (DisplayName notifica).
                    collection.Label = r.Content?.Label;
                    // Não refiltra a lista aqui: trocar a lista filtrada pode desfazer a seleção atual. O filtro
                    // ativo passa a considerar o nome novo na próxima vez que o texto da pesquisa mudar.
                    if (_labelEditClassCode == collection.classCode)
                        CancelEditLabel();
                }
                else if (_labelEditClassCode == collection.classCode)
                {
                    LabelEditError = string.IsNullOrWhiteSpace(r?.message) ? Loc.Tr("Could not rename the collection.") : r.message;
                }
            });
        }
        catch (Exception e)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_labelEditClassCode == collection.classCode)
                    LabelEditError = Loc.Tr("Could not rename the collection.") + " " + e.Message;
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsSavingLabel = false);
        }
    }
}
