using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedClientSide.ServerInteraction.Items.Financial;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Collections;

// Tela "Itens de cobrança" da coleção selecionada (Views/Collections/CollectionInvoiceItems.axaml):
// uma linha por item — consolidados (já entraram numa fatura fechada) e prévias (podem mudar até o
// fechamento do ciclo mensal) — do mais recente pro mais antigo. Ordem e regra de quais prévias
// aparecem vêm prontas do servidor (PgService.GetCollectionInvoiceItemsForClient).
public partial class CollectionsViewModel
{
    [ObservableProperty] private ObservableCollection<CollectionInvoiceItemRow> collectionInvoiceItems = new();
    [ObservableProperty] private bool collectionInvoiceItemsIsLoading;
    [ObservableProperty] private string? collectionInvoiceItemsError;
    [ObservableProperty] private string collectionInvoiceItemsConsolidatedTotalText = FormatCurrency(0);
    [ObservableProperty] private string collectionInvoiceItemsPreviewTotalText = FormatCurrency(0);
    [ObservableProperty] private bool collectionInvoiceItemsHasPreview;

    public bool CollectionInvoiceItemsIsEmpty =>
        !CollectionInvoiceItemsIsLoading && CollectionInvoiceItemsError == null && CollectionInvoiceItems.Count == 0;
    public bool CollectionInvoiceItemsHasRows => CollectionInvoiceItems.Count > 0;

    partial void OnCollectionInvoiceItemsIsLoadingChanged(bool value) => OnPropertyChanged(nameof(CollectionInvoiceItemsIsEmpty));
    partial void OnCollectionInvoiceItemsErrorChanged(string? value) => OnPropertyChanged(nameof(CollectionInvoiceItemsIsEmpty));
    partial void OnCollectionInvoiceItemsChanged(ObservableCollection<CollectionInvoiceItemRow> value)
    {
        OnPropertyChanged(nameof(CollectionInvoiceItemsIsEmpty));
        OnPropertyChanged(nameof(CollectionInvoiceItemsHasRows));
    }

    [RelayCommand]
    public async Task OpenCollectionInvoiceItemsViewCommand()
    {
        // Mantém SelectedCollection (igual OpenCancelBillingViewCommand) — a tela mostra a coleção atual.
        ActiveComponent = ActiveViews.CollectionInvoiceItems;
        await LoadCollectionInvoiceItemsAsync();
    }

    [RelayCommand]
    public async Task RefreshCollectionInvoiceItemsCommand() => await LoadCollectionInvoiceItemsAsync();

    private async Task LoadCollectionInvoiceItemsAsync()
    {
        var classCode = SelectedCollection?.classCode;
        if (string.IsNullOrEmpty(classCode))
            return;

        CollectionInvoiceItemsIsLoading = true;
        CollectionInvoiceItemsError = null;
        CollectionInvoiceItems = new ObservableCollection<CollectionInvoiceItemRow>();
        try
        {
            var result = await GlobalAppStateViewModel.lfc.GetCollectionInvoiceItems(classCode);

            // O usuário pode ter trocado de coleção enquanto a requisição estava em andamento.
            if (SelectedCollection?.classCode != classCode)
                return;

            if (result == null || result.success != true || result.Content == null)
            {
                CollectionInvoiceItemsError = result?.message ?? Loc.Tr("Could not load the billing items of this collection.");
                return;
            }

            var rows = result.Content.Select(x => new CollectionInvoiceItemRow(x)).ToList();
            CollectionInvoiceItems = new ObservableCollection<CollectionInvoiceItemRow>(rows);

            var billable = rows.Where(x => !x.IsCancelled).ToList();
            CollectionInvoiceItemsConsolidatedTotalText = FormatCurrency(billable.Where(x => !x.IsPreview).Sum(x => x.Dto.Value ?? 0));
            CollectionInvoiceItemsPreviewTotalText = FormatCurrency(billable.Where(x => x.IsPreview).Sum(x => x.Dto.Value ?? 0));
            CollectionInvoiceItemsHasPreview = rows.Any(x => x.IsPreview);
        }
        catch (Exception e)
        {
            CollectionInvoiceItemsError = Loc.Tr("Could not load the billing items of this collection.") + " " + e.Message;
        }
        finally
        {
            CollectionInvoiceItemsIsLoading = false;
        }
    }

    internal static string FormatCurrency(double value) =>
        value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
}

// Uma linha da tabela — só formatação do DTO pra exibição.
public class CollectionInvoiceItemRow
{
    public CollectionInvoiceItemDTO Dto { get; }

    public CollectionInvoiceItemRow(CollectionInvoiceItemDTO dto)
    {
        Dto = dto;
    }

    public bool IsPreview => Dto.IsPreview;
    public bool IsCancelled => Dto.BillingCancelled == true;
    public bool IsConsolidated => !IsPreview && !IsCancelled;
    public bool IsPreviewAndNotCancelled => IsPreview && !IsCancelled;

    public string PeriodText => Dto.StartDate?.ToString("MM/yyyy", CultureInfo.InvariantCulture) ?? "-";

    // Data/hora do lançamento (InvoiceItemCreationDate), no fuso local. Vem do servidor em UTC; se
    // chegar sem Kind, trata como UTC. Na prévia é a última atualização (ela é regravada a cada
    // mudança até o fechamento).
    public string CreationDateTimeText
    {
        get
        {
            if (Dto.CreationDate is not DateTime dt)
                return "";
            if (dt.Kind == DateTimeKind.Unspecified)
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }
    }

    public string CreationDateTimeTooltip => IsPreview
        ? Loc.Tr("Last update of this preview")
        : Loc.Tr("Date and time the item was registered");

    public string TypeText => Dto.InvoiceType switch
    {
        "NORMAL" => Loc.Tr("Collection services"),
        "OLD_STORAGE" => Loc.Tr("HD storage"),
        "EXTENSION_DELETE_DATE" => Loc.Tr("Deadline extension"),
        _ => Dto.InvoiceType ?? "-"
    };

    public string DescriptionText
    {
        get
        {
            if (Dto.InvoiceType == "EXTENSION_DELETE_DATE" && Dto.DeletionDeadlineExtensionDays is int days && days > 0)
                return string.Format(Loc.Tr("{0} extra days"), days);
            return string.IsNullOrWhiteSpace(Dto.Description) ? "-" : Dto.Description.Trim();
        }
    }

    // Na extensão de prazo, a base do valor são as fotos da coleção quando a extensão foi pedida, não
    // as enviadas no mês (NumPhotos).
    public string PhotosText => (Dto.InvoiceType == "EXTENSION_DELETE_DATE" ? Dto.PhotoCountAtExtensionGeneration : Dto.NumPhotos)
        ?.ToString("N0", CultureInfo.GetCultureInfo("pt-BR")) ?? "-";

    public string ValueText => CollectionsViewModel.FormatCurrency(Dto.Value ?? 0);

    public string StatusText => IsCancelled
        ? Loc.Tr("Canceled item")
        : IsPreview ? Loc.Tr("Preview") : Loc.Tr("Consolidated");

    public string StatusTooltip => IsCancelled
        ? Loc.Tr("This item will not be charged.")
        : IsPreview
            ? Loc.Tr("Preview: the value may change until the monthly billing cycle closes.")
            : Loc.Tr("Consolidated: this item is already part of a closed invoice.");
}
