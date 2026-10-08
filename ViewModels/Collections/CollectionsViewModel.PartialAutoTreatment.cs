using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedClientSide.ServerInteraction.Items.AutoTreatment;
using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Collections;

// Tratamento parcial (Views/Collections/CollectionPartialAutoTreatment.axaml + bloco de progresso em
// CollectionView): coleções HD sem auto tratamento completo pedem lotes por participante e/ou por pasta.
// O servidor calcula o lote (tira as já tratadas e as já pedidas) — a tela só escolhe, simula
// (DryRun) e confirma. Pedido não pode ser desfeito: as fotos vão pra fila na hora.
public partial class CollectionsViewModel
{
    // ---- Visão geral / progresso ----

    [ObservableProperty] private PartialAutoTreatmentOverviewDTO? partialOverview;
    private string? _partialOverviewClassCode;
    // Carregando a visão geral pela 1ª vez (sem números ainda) e erro da última tentativa — a tela mostra
    // um ou outro em vez de ficar em branco quando o endpoint falha.
    [ObservableProperty] private bool partialOverviewLoading;
    [ObservableProperty] private string? partialOverviewError;
    public bool PartialOverviewLoadingFirstTime => PartialOverviewLoading && PartialOverview == null;
    public bool PartialHasOverview => PartialOverview != null;
    partial void OnPartialOverviewLoadingChanged(bool value) => OnPropertyChanged(nameof(PartialOverviewLoadingFirstTime));

    // Modo parcial: coleção HD sem auto tratamento completo (o completo segue com a barra antiga).
    public bool IsPartialAutoTreatmentMode =>
        SelectedCollection?.UploadHD == true && SelectedCollection?.AutoTreatment != true;

    public int PartialTotal => PartialOverview?.TotalPhotos ?? 0;
    public int PartialTreated => PartialOverview?.TreatedPhotos ?? 0;
    public int PartialRequested => PartialOverview?.RequestedPhotos ?? 0;
    public int PartialPending => PartialOverview?.RequestedPendingPhotos ?? 0;
    public int PartialAvailable => PartialOverview?.AvailablePhotos ?? 0;
    // Barra empilhada: o fundo vai até tratadas+na fila (âmbar), a de cima até as tratadas (verde).
    public int PartialTreatedPlusPending => PartialTreated + PartialPending;
    // Evita ProgressBar com Maximum 0.
    public int PartialBarMaximum => Math.Max(PartialTotal, 1);
    public bool PartialHasBatches => PartialOverview?.Batches?.Count > 0;
    public bool PartialIsEligible => PartialOverview?.IsEligible == true;
    public string? PartialNotEligibleReason => PartialOverview?.IsEligible == false ? PartialOverview.NotEligibleReason : null;
    public string PartialPercentText => PartialTotal == 0 ? "0%" : (100.0 * PartialTreated / PartialTotal).ToString("0", CultureInfo.InvariantCulture) + "%";

    partial void OnPartialOverviewChanged(PartialAutoTreatmentOverviewDTO? value)
    {
        foreach (var name in new[]
        {
            nameof(PartialTotal), nameof(PartialTreated), nameof(PartialRequested), nameof(PartialPending),
            nameof(PartialAvailable), nameof(PartialTreatedPlusPending), nameof(PartialBarMaximum),
            nameof(PartialHasBatches), nameof(PartialIsEligible), nameof(PartialNotEligibleReason), nameof(PartialPercentText),
            nameof(PartialOverviewLoadingFirstTime), nameof(PartialHasOverview),
        })
            OnPropertyChanged(name);
        RebuildPartialFolders();
        PartialBatches = new ObservableCollection<PartialBatchRow>((value?.Batches ?? new List<PartialAutoTreatmentBatchDTO>()).Select(x => new PartialBatchRow(x)));
    }

    // Chamado por UpdateProgressBars (timer e seleção de coleção).
    private async Task RefreshPartialAutoTreatmentOverviewAsync(ProfessionalTask selected)
    {
        OnPropertyChanged(nameof(IsPartialAutoTreatmentMode));
        // Trocou de coleção: não mostra os números da anterior enquanto carrega.
        if (_partialOverviewClassCode != selected.classCode)
        {
            PartialOverview = null;
            PartialOverviewError = null;
        }
        _partialOverviewClassCode = selected.classCode;
        if (selected.UploadHD != true || selected.AutoTreatment == true)
        {
            PartialOverview = null;
            PartialOverviewError = null;
            return;
        }
        PartialOverviewLoading = true;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.GetPartialAutoTreatmentOverview(selected.classCode);
            if (SelectedCollection?.classCode != selected.classCode)
                return;
            if (r?.success == true && r.Content != null)
            {
                PartialOverview = r.Content;
                PartialOverviewError = null;
            }
            else
            {
                PartialOverviewError = r?.message ?? Loc.Tr("Could not load the partial treatment of this collection.");
            }
        }
        catch (Exception e)
        {
            // Não trava o resto da tela — mas mostra o erro no bloco do tratamento parcial.
            if (SelectedCollection?.classCode == selected.classCode)
                PartialOverviewError = Loc.Tr("Could not load the partial treatment of this collection.") + " " + e.Message;
        }
        finally
        {
            PartialOverviewLoading = false;
        }
    }

    // ---- Ícone da lista ----

    // classCodes das coleções com ao menos um lote (ícone PartialAutoTreatmentIcon em CollectionList, via
    // CollectionHasPartialAutoTreatmentConverter). Trocado por um conjunto novo a cada carga pra disparar o
    // MultiBinding das linhas.
    [ObservableProperty] private ISet<string> partialAutoTreatmentClassCodes = new HashSet<string>();

    // Toda vez que alguma das listas de coleções é trocada (carga inicial, "carregar mais", atualização,
    // abas de vencidas/deletadas), busca de novo quais têm lote — 1 chamada pra company inteira. Chamadas
    // em sequência rápida (várias listas trocadas juntas) viram uma só.
    partial void OnCollectionsListChanged(ObservableCollection<ProfessionalTask> value) => RequestPartialAutoTreatmentClassCodesRefresh();

    private int _partialCodesRefreshRunning;
    private void RequestPartialAutoTreatmentClassCodesRefresh()
    {
        if (Interlocked.Exchange(ref _partialCodesRefreshRunning, 1) == 1)
            return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300);
                await RefreshPartialAutoTreatmentClassCodesAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _partialCodesRefreshRunning, 0);
            }
        });
    }

    private async Task RefreshPartialAutoTreatmentClassCodesAsync()
    {
        try
        {
            var r = await GlobalAppStateViewModel.lfc.GetCollectionsWithPartialAutoTreatment();
            if (r?.success == true && r.Content != null)
                await Dispatcher.UIThread.InvokeAsync(() => PartialAutoTreatmentClassCodes = new HashSet<string>(r.Content));
        }
        catch
        {
            // Ícone é informativo — falha aqui não afeta a lista.
        }
    }

    // ---- Tela de pedido ----

    [ObservableProperty] private ObservableCollection<PartialFolderRow> partialFolders = new();
    [ObservableProperty] private ObservableCollection<PartialParticipantRow> partialParticipants = new();
    [ObservableProperty] private ObservableCollection<PartialParticipantRow> partialParticipantsFiltered = new();
    [ObservableProperty] private ObservableCollection<PartialBatchRow> partialBatches = new();
    [ObservableProperty] private string partialParticipantSearch = string.Empty;
    [ObservableProperty] private bool partialParticipantsLoading;
    [ObservableProperty] private string? partialParticipantsError;

    [ObservableProperty] private PartialAutoTreatmentRequestResultDTO? partialSimulation;
    [ObservableProperty] private bool partialSimulating;
    [ObservableProperty] private bool partialRequesting;
    [ObservableProperty] private string? partialMessage;

    // Aba "Por participante": a lista inteira aparece logo abaixo da busca (a busca só filtra).
    public string PartialParticipantsCountText => string.Format(Loc.Tr("{0} participant(s) in this collection"), PartialParticipants.Count.ToString("N0", PtBr));
    public bool PartialParticipantsEmpty => !PartialParticipantsLoading && PartialParticipantsError == null && PartialParticipants.Count == 0;
    public bool PartialParticipantsHasRows => PartialParticipants.Count > 0;
    partial void OnPartialParticipantsChanged(ObservableCollection<PartialParticipantRow> value) => NotifyPartialParticipantsState();
    partial void OnPartialParticipantsLoadingChanged(bool value) => NotifyPartialParticipantsState();
    partial void OnPartialParticipantsErrorChanged(string? value) => NotifyPartialParticipantsState();
    private void NotifyPartialParticipantsState()
    {
        OnPropertyChanged(nameof(PartialParticipantsCountText));
        OnPropertyChanged(nameof(PartialParticipantsEmpty));
        OnPropertyChanged(nameof(PartialParticipantsHasRows));
    }

    // Detalhamento do lote simulado: 1 linha por participante/pasta escolhido.
    public ObservableCollection<PartialSelectionItemRow> PartialSimulationItems =>
        new((PartialSimulation?.Selection ?? new List<PartialAutoTreatmentSelectionItemDTO>()).Select(x => new PartialSelectionItemRow(x)));

    public int PartialSelectedFoldersCount => PartialFolders.Count(x => x.IsSelected);
    public int PartialSelectedParticipantsCount => PartialParticipants.Count(x => x.IsSelected);
    public bool PartialHasSelection => PartialSelectedFoldersCount + PartialSelectedParticipantsCount > 0;
    public bool PartialHasSelectedFolders => PartialSelectedFoldersCount > 0;
    public bool PartialHasSelectedParticipants => PartialSelectedParticipantsCount > 0;
    public bool PartialCanRequest => PartialHasSelection && !PartialSimulating && !PartialRequesting && PartialSimulation?.BillablePhotoCount > 0 && PartialIsEligible;
    public bool PartialHasSimulation => PartialSimulation != null && PartialHasSelection;
    // Seleção válida, mas nenhuma foto nova: tudo já tratado ou já pedido num lote anterior.
    public bool PartialSimulationNothingToTreat => PartialHasSimulation && !PartialSimulating && PartialSimulation!.BillablePhotoCount == 0;

    public string PartialSimulationSelectedText => (PartialSimulation?.SelectedPhotoCount ?? 0).ToString("N0", PtBr);
    public string PartialSimulationTreatedText => (PartialSimulation?.AlreadyTreatedPhotoCount ?? 0).ToString("N0", PtBr);
    public string PartialSimulationRequestedText => (PartialSimulation?.AlreadyRequestedPhotoCount ?? 0).ToString("N0", PtBr);
    public string PartialSimulationBillableText => (PartialSimulation?.BillablePhotoCount ?? 0).ToString("N0", PtBr);
    public string PartialSimulationValueText => FormatCurrency((PartialSimulation?.TotalValueCents ?? 0) / 100);
    public bool PartialSimulationFreeTrial => PartialSimulation?.RequestedDuringFreeTrial == true;
    public string? PartialSimulationCpfsNotFoundText => PartialSimulation?.CpfsNotFound?.Count > 0
        ? string.Format(Loc.Tr("{0} selected participant(s) have no photos in this collection."), PartialSimulation.CpfsNotFound.Count)
        : null;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private CancellationTokenSource? _partialSimulationCts;

    partial void OnPartialSimulationChanged(PartialAutoTreatmentRequestResultDTO? value) => NotifyPartialSelectionState();
    partial void OnPartialSimulatingChanged(bool value) => NotifyPartialSelectionState();
    partial void OnPartialRequestingChanged(bool value) => NotifyPartialSelectionState();
    partial void OnPartialParticipantSearchChanged(string value) => ApplyPartialParticipantFilter();

    private void NotifyPartialSelectionState()
    {
        foreach (var name in new[]
        {
            nameof(PartialSelectedFoldersCount), nameof(PartialSelectedParticipantsCount), nameof(PartialHasSelection),
            nameof(PartialHasSelectedFolders), nameof(PartialHasSelectedParticipants),
            nameof(PartialCanRequest), nameof(PartialHasSimulation), nameof(PartialSimulationSelectedText),
            nameof(PartialSimulationTreatedText), nameof(PartialSimulationRequestedText), nameof(PartialSimulationBillableText),
            nameof(PartialSimulationValueText), nameof(PartialSimulationFreeTrial), nameof(PartialSimulationCpfsNotFoundText),
            nameof(PartialSimulationItems), nameof(PartialSimulationNothingToTreat),
        })
            OnPropertyChanged(name);
    }

    // Recria as linhas de pasta a partir da visão geral, preservando o que estava marcado.
    private void RebuildPartialFolders()
    {
        var selected = PartialFolders.Where(x => x.IsSelected).Select(x => x.Folder).ToHashSet();
        PartialFolders = new ObservableCollection<PartialFolderRow>(
            (PartialOverview?.Folders ?? new List<PartialAutoTreatmentFolderDTO>())
                .Select(x => new PartialFolderRow(x, selected.Contains(x.Folder) && x.AvailablePhotos > 0, OnPartialSelectionChanged)));
        NotifyPartialSelectionState();
    }

    private void ApplyPartialParticipantFilter()
    {
        var term = (PartialParticipantSearch ?? string.Empty).Trim();
        var digits = new string(term.Where(char.IsDigit).ToArray());
        PartialParticipantsFiltered = new ObservableCollection<PartialParticipantRow>(string.IsNullOrEmpty(term)
            ? PartialParticipants
            : PartialParticipants.Where(x =>
                (x.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (digits.Length > 0 && (x.Cpf?.Contains(digits) ?? false))));
    }

    [RelayCommand]
    public async Task OpenPartialAutoTreatmentViewCommand()
    {
        if (SelectedCollection == null)
            return;
        PartialSimulation = null;
        PartialMessage = null;
        PartialParticipantSearch = string.Empty;
        PartialFolders = new ObservableCollection<PartialFolderRow>();
        PartialParticipants = new ObservableCollection<PartialParticipantRow>();
        PartialParticipantsFiltered = new ObservableCollection<PartialParticipantRow>();
        ActiveComponent = ActiveViews.PartialAutoTreatment;

        await RefreshPartialAutoTreatmentOverviewAsync(SelectedCollection);
        await LoadPartialParticipantsAsync();
    }

    [RelayCommand]
    public async Task RefreshPartialAutoTreatmentCommand()
    {
        if (SelectedCollection != null)
            await RefreshPartialAutoTreatmentOverviewAsync(SelectedCollection);
    }

    [RelayCommand]
    public void ClearPartialSelectionCommand()
    {
        foreach (var f in PartialFolders) f.SetSelectedSilently(false);
        foreach (var p in PartialParticipants) p.SetSelectedSilently(false);
        PartialSimulation = null;
        PartialMessage = null;
        NotifyPartialSelectionState();
    }

    // Marca/desmarca de uma vez os participantes visíveis (respeita o filtro da busca) — 1 simulação só no fim.
    [RelayCommand]
    public void SelectAllVisiblePartialParticipantsCommand() => SetVisiblePartialParticipants(true);

    [RelayCommand]
    public void UnselectAllVisiblePartialParticipantsCommand() => SetVisiblePartialParticipants(false);

    private void SetVisiblePartialParticipants(bool selected)
    {
        foreach (var p in PartialParticipantsFiltered)
            p.SetSelectedSilently(selected);
        OnPartialSelectionChanged();
    }

    private async Task LoadPartialParticipantsAsync()
    {
        var classCode = SelectedCollection?.classCode;
        if (string.IsNullOrEmpty(classCode))
            return;
        PartialParticipantsLoading = true;
        PartialParticipantsError = null;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.GetPartialAutoTreatmentParticipants(classCode);
            if (SelectedCollection?.classCode != classCode)
                return;
            if (r?.success != true || r.Content == null)
            {
                PartialParticipantsError = r?.message ?? Loc.Tr("Could not load the participants of this collection.");
                return;
            }
            PartialParticipants = new ObservableCollection<PartialParticipantRow>(r.Content.Select(x => new PartialParticipantRow(x, OnPartialSelectionChanged)));
            ApplyPartialParticipantFilter();
        }
        catch (Exception e)
        {
            PartialParticipantsError = Loc.Tr("Could not load the participants of this collection.") + " " + e.Message;
        }
        finally
        {
            PartialParticipantsLoading = false;
        }
    }

    // Toda mudança de seleção re-simula (com um pequeno atraso pra juntar cliques seguidos). O resultado da
    // seleção anterior é descartado NA HORA: sem isso, durante o atraso o botão "Solicitar" ficava habilitado
    // com as contas de outra seleção. O botão só habilita com uma simulação da seleção atual que tenha foto
    // nova (nem tratada nem já pedida) — e o servidor recalcula tudo de novo no pedido real.
    private void OnPartialSelectionChanged()
    {
        PartialSimulation = null;
        PartialMessage = null;
        PartialSimulating = PartialHasSelection;
        NotifyPartialSelectionState();
        _partialSimulationCts?.Cancel();
        _partialSimulationCts = new CancellationTokenSource();
        var token = _partialSimulationCts.Token;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(400, token); }
            catch (TaskCanceledException) { return; }
            await Dispatcher.UIThread.InvokeAsync(() => SimulatePartialAutoTreatmentAsync(token));
        });
    }

    private PartialAutoTreatmentRequestDTO BuildPartialRequest(bool dryRun) => new()
    {
        ClassCode = SelectedCollection?.classCode,
        Folders = PartialFolders.Where(x => x.IsSelected).Select(x => x.Folder).ToList(),
        Cpfs = PartialParticipants.Where(x => x.IsSelected).Select(x => x.Cpf).ToList(),
        DryRun = dryRun,
        Source = "Dashboard",
    };

    private async Task SimulatePartialAutoTreatmentAsync(CancellationToken token)
    {
        if (!PartialHasSelection)
        {
            PartialSimulation = null;
            PartialSimulating = false;
            return;
        }
        PartialSimulating = true;
        PartialMessage = null;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.RequestPartialAutoTreatment(BuildPartialRequest(dryRun: true));
            if (token.IsCancellationRequested)
                return;
            PartialSimulation = r?.Content;
            if (r?.success != true)
                PartialMessage = r?.message;
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                PartialMessage = e.Message;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                PartialSimulating = false;
        }
    }

    [RelayCommand]
    public async Task RequestPartialAutoTreatmentCommand()
    {
        if (!PartialCanRequest || PartialSimulation == null)
            return;

        var msg = string.Format(
            Loc.Tr("Request the treatment of {0} photo(s) for {1}?\n\nThe photos go to the treatment queue right away and the request cannot be cancelled."),
            PartialSimulation.BillablePhotoCount.ToString("N0", PtBr),
            PartialSimulationValueText);
        if (!await GlobalAppStateViewModel.Instance.ShowDialogYesNo(msg, Loc.Tr("Partial treatment")))
            return;

        PartialRequesting = true;
        PartialMessage = null;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.RequestPartialAutoTreatment(BuildPartialRequest(dryRun: false));
            if (r?.success == true)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(string.Format(
                    Loc.Tr("{0} photo(s) were sent to the treatment queue."),
                    (r.Content?.BillablePhotoCount ?? 0).ToString("N0", PtBr)));
                ClearPartialSelectionCommand();
                // A coleção pode ter acabado de ganhar o 1º lote — atualiza o ícone da lista.
                _ = RefreshPartialAutoTreatmentClassCodesAsync();
            }
            else
            {
                PartialMessage = r?.message ?? Loc.Tr("Could not request the treatment.");
                if (r?.Content != null)
                    PartialSimulation = r.Content;
            }
        }
        catch (Exception e)
        {
            PartialMessage = Loc.Tr("Could not request the treatment.") + " " + e.Message;
        }
        finally
        {
            PartialRequesting = false;
            if (SelectedCollection != null)
                await RefreshPartialAutoTreatmentOverviewAsync(SelectedCollection);
        }
    }
}

// Uma pasta selecionável. Sem fotos disponíveis (tudo tratado/pedido) não dá pra marcar.
public partial class PartialFolderRow : ObservableObject
{
    private readonly Action _onChanged;
    private bool _silent;

    public PartialFolderRow(PartialAutoTreatmentFolderDTO dto, bool selected, Action onChanged)
    {
        Dto = dto;
        _onChanged = onChanged;
        _silent = true;
        IsSelected = selected;
        _silent = false;
    }

    public PartialAutoTreatmentFolderDTO Dto { get; }
    public string Folder => Dto.Folder;
    public bool CanSelect => Dto.AvailablePhotos > 0;
    public string TotalText => Dto.TotalPhotos.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
    public string TreatedText => Dto.TreatedPhotos.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
    public string PendingText => Dto.RequestedPendingPhotos.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
    public string AvailableText => Dto.AvailablePhotos.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));

    [ObservableProperty] private bool isSelected;
    partial void OnIsSelectedChanged(bool value)
    {
        if (!_silent)
            _onChanged();
    }

    public void SetSelectedSilently(bool value)
    {
        _silent = true;
        IsSelected = value;
        _silent = false;
    }
}

public partial class PartialParticipantRow : ObservableObject
{
    private readonly Action _onChanged;
    private bool _silent;

    public PartialParticipantRow(PartialAutoTreatmentParticipantDTO dto, Action onChanged)
    {
        Cpf = dto.Cpf;
        Name = dto.Name;
        _onChanged = onChanged;
    }

    public string Cpf { get; }
    public string Name { get; }
    // CPF mascarado na tela (***.456.789-**), o completo só vai pro servidor.
    public string CpfMasked => Cpf?.Length == 11 ? $"***.{Cpf.Substring(3, 3)}.{Cpf.Substring(6, 3)}-**" : Cpf ?? "";

    [ObservableProperty] private bool isSelected;
    partial void OnIsSelectedChanged(bool value)
    {
        if (!_silent)
            _onChanged();
    }

    public void SetSelectedSilently(bool value)
    {
        _silent = true;
        IsSelected = value;
        _silent = false;
    }
}

public class PartialBatchRow
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public PartialBatchRow(PartialAutoTreatmentBatchDTO dto) => Dto = dto;
    public PartialAutoTreatmentBatchDTO Dto { get; }

    public string DateText => DateTime.SpecifyKind(Dto.CreatedAt, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public string RequestedByText => string.IsNullOrEmpty(Dto.RequestedBy) ? "-" : Dto.RequestedBy;
    public string SelectionText
    {
        get
        {
            var parts = new List<string>();
            if (Dto.SelectedFolders?.Count > 0)
                parts.Add(string.Join(", ", Dto.SelectedFolders));
            if (Dto.SelectedParticipantsCount > 0)
                parts.Add(string.Format(Loc.Tr("{0} participant(s)"), Dto.SelectedParticipantsCount));
            return parts.Count == 0 ? "-" : string.Join(" + ", parts);
        }
    }
    // Detalhamento gravado no lote (a qual participante/pasta ele se destina).
    public List<PartialSelectionItemRow> Items => (Dto.Selection ?? new List<PartialAutoTreatmentSelectionItemDTO>()).Select(x => new PartialSelectionItemRow(x)).ToList();
    public bool HasItems => Dto.Selection?.Count > 0;
    public string BillableText => Dto.BillablePhotoCount.ToString("N0", PtBr);
    public string SkippedText => Dto.SkippedPhotoCount.ToString("N0", PtBr);
    public string ValueText => CollectionsViewModel.FormatCurrency((Dto.TotalValueCents ?? 0) / 100);
    public bool EnqueueFailed => Dto.Status == "EnqueueFailed";
}

// Um item do detalhamento da seleção (participante ou pasta) — usado no resumo do lote e no histórico.
public class PartialSelectionItemRow
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public PartialSelectionItemRow(PartialAutoTreatmentSelectionItemDTO dto) => Dto = dto;
    public PartialAutoTreatmentSelectionItemDTO Dto { get; }

    public bool IsFolder => Dto.Type == "Folder";
    // Ícone Segoe MDL2: pasta / pessoa.
    public string Icon => IsFolder ? "" : "";
    public string Title => IsFolder
        ? Dto.Folder ?? "-"
        : $"{(string.IsNullOrWhiteSpace(Dto.ParticipantName) ? Loc.Tr("Participant") : Dto.ParticipantName)} ({MaskCpf(Dto.Cpf)})";

    // "N fotos · Z já tratadas · W já em lote → Y no lote" (ou "sem fotos nesta coleção").
    public string DetailText => Dto.NotFound
        ? Loc.Tr("no photos in this collection")
        : string.Format(Loc.Tr("{0} photo(s) · {1} already treated · {2} already in a batch → {3} in this batch"),
            Dto.SelectedPhotoCount.ToString("N0", PtBr),
            Dto.AlreadyTreatedPhotoCount.ToString("N0", PtBr),
            Dto.AlreadyRequestedPhotoCount.ToString("N0", PtBr),
            Dto.BillablePhotoCount.ToString("N0", PtBr));

    private static string MaskCpf(string? cpf) =>
        cpf?.Length == 11 ? $"***.{cpf.Substring(3, 3)}.{cpf.Substring(6, 3)}-**" : cpf ?? "";
}
