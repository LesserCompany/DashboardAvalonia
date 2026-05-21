using Avalonia.Interactivity;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExCSS;
using JavaScriptCore;
using LesserDashboardClient.Models;
using LesserDashboardClient.ViewModels.SearchGraduate;
using LesserDashboardClient.Views;
using LesserDashboardClient.Views.Collections;
using LesserDashboardClient.Views.SearchGraduate;
using MsBox.Avalonia;
using Newtonsoft.Json;
using OfficeOpenXml;
using SharedClientSide;
using SharedClientSide.DataStructure.CoreApp;
using SharedClientSide.Helpers;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Companies.Requests;
using SharedClientSide.ServerInteraction.Users.Graduate;
using SharedClientSide.ServerInteraction.Users.Professionals;
using SharedClientSide.ServerInteraction.Users.Requests;
using SharedClientSide.ServerInteraction.Users.Results;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LesserDashboardClient.Services;


namespace LesserDashboardClient.ViewModels.Collections;

public partial class CollectionsViewModel : ViewModelBase
{
    public static CollectionsViewModel Instance { get; set; }
    private static Queue<string> CollectionCreationQueue = new Queue<string>();

    public class GraduateReportInfo
    {
        public string Name { get; set; } = "";
        public int BlueTags { get; set; } = 0;
        public int RedTags { get; set; } = 0;
        public int TotalTags
        {
            get
            {
                return BlueTags + RedTags;
            }
        }
    }

    #region VIEWS
    // ======================== VIEWS ======================== 

    private CancellationTokenSource _previewDebounceCtsViewCollection;
    private CancellationTokenSource _filterDebounceCts;
    private bool _isUpdatingSelectedCollection = false;
    private bool _isLoadingReuploadData = false; // Flag para prevenir eventos durante carregamento de reupload
    private bool _isOpeningSelectProfessionalView = false; // Evita fechar a lista no 1Âº clique (quando LoadProfessionals define SelectedProfessional)
    /// <summary>Combo da coleÃ§Ã£o prÃ©-configurada; ao criar turma, a PT deve espelhar estes valores (autoridade mÃ¡xima).</summary>
    private CollectionComboOptions _preConfiguredComboAuthority;
    [ObservableProperty] public ProfessionalTask selectedCollection;
    
    /// <summary>
    /// Verifica se faltam 30 dias ou menos para a deleÃ§Ã£o
    /// </summary>
    public bool IsDeletionDateNear => SelectedCollection?.ScheduledDeletionDate != null && 
        (SelectedCollection.ScheduledDeletionDate.Value - DateTimeOffset.Now).TotalDays <= 30;
    
    /// <summary>
    /// Retorna a cor do texto da data de deleÃ§Ã£o (vermelho vibrante se <= 30 dias)
    /// </summary>
    public Avalonia.Media.IBrush DeletionDateForeground
    {
        get
        {
            if (IsDeletionDateNear)
            {
                // Vermelho vibrante para alerta
                return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 50, 50));
            }
            // Cor padrÃ£o (WarningTextBrush)
            return Avalonia.Application.Current?.Resources["WarningTextBrush"] as Avalonia.Media.IBrush 
                ?? Avalonia.Media.Brushes.Orange;
        }
    }
    
    /// <summary>
    /// Indica se deve mostrar o Ã­cone de alerta
    /// </summary>
    public bool ShowDeletionDateAlertIcon => IsDeletionDateNear;
    
    /// <summary>
    /// Retorna o peso da fonte para a data de deleÃ§Ã£o (Bold se <= 30 dias)
    /// </summary>
    public Avalonia.Media.FontWeight DeletionDateFontWeight => IsDeletionDateNear 
        ? Avalonia.Media.FontWeight.Bold 
        : Avalonia.Media.FontWeight.Normal;
    
    /// <summary>
    /// Verifica se a data de vencimento jÃ¡ passou (para habilitar botÃ£o de deletar turma)
    /// </summary>
    public bool IsDeletionDatePassed => SelectedCollection?.ScheduledDeletionDate != null && 
        SelectedCollection.ScheduledDeletionDate.Value <= DateTimeOffset.Now;

    /// <summary>Exibe "Deletar coleÃ§Ã£o" para coleÃ§Ãµes HD (exceto na lista de deletadas).</summary>
    public bool IsDeleteCollectionButtonVisible =>
        !IsSelectedCollectionInDeletedList &&
        SelectedCollection?.UploadHD == true;

    /// <summary>Habilita "Deletar coleÃ§Ã£o" apenas quando estiver na aba Vencidas e a data de deleÃ§Ã£o jÃ¡ passou.</summary>
    public bool IsDeleteCollectionButtonEnabled =>
        IsSelectedCollectionInExpiredList && IsDeletionDatePassed;

    partial void OnSelectedCollectionChanged(ProfessionalTask value)
    {
        if (_isUpdatingSelectedCollection)
            return;

        // SeleÃ§Ã£o ficou null na CollectionView: troca estado primeiro (NewsView), depois a UI atualiza sem frame vazio
        if (value == null && ActiveComponent == ActiveViews.CollectionView)
        {
            _isUpdatingSelectedCollection = true;
            try { ActiveComponent = ActiveViews.NewsView; }
            finally { _isUpdatingSelectedCollection = false; }
            return;
        }

        if (value != null)
            OnPropertyChanged(nameof(ComponentCollectionViewIsVisibleAndSafe));

        _previewDebounceCtsViewCollection?.Cancel();
        _previewDebounceCtsViewCollection = new();
        var token = _previewDebounceCtsViewCollection.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100, token); // Reduzido de 250ms para 100ms
                if (token.IsCancellationRequested)
                    return;
                
                // Marcar que estamos atualizando para evitar loops
                _isUpdatingSelectedCollection = true;
                
                // dispara carregamentos com base no ActiveCollection
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateCollectionViewSelected();
                    // Notificar mudanÃ§as nas propriedades calculadas da data de deleÃ§Ã£o
                    OnPropertyChanged(nameof(IsDeletionDateNear));
                    OnPropertyChanged(nameof(DeletionDateForeground));
                    OnPropertyChanged(nameof(ShowDeletionDateAlertIcon));
                    OnPropertyChanged(nameof(IsDeletionDatePassed));
                    OnPropertyChanged(nameof(IsDeleteCollectionButtonVisible));
                    OnPropertyChanged(nameof(IsDeleteCollectionButtonEnabled));
                    NotifyDeletedCollectionViewState();
                });
            }
            catch (OperationCanceledException) { }
            finally
            {
                _isUpdatingSelectedCollection = false;
            }
        }, token);
    }

    [RelayCommand]
    private async Task CopyCollectionCpfsAsync(ProfessionalTask professionalTask)
    {
        if (professionalTask == null || string.IsNullOrWhiteSpace(professionalTask.classCode))
        {
            await MessageBoxManager.GetMessageBoxStandard(
                "Erro",
                "ColeÃ§Ã£o invÃ¡lida: classCode ausente.",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
            return;
        }

        if (CopyingCollectionCpfs || ManagingCollectionCpfs)
            return;

        CopyingCollectionCpfs = true;
        try
        {
            var result = await GlobalAppStateViewModel.lfc.GetGraduatesByCPFByClassCode(professionalTask.classCode);
            if (!result.success || result.Content == null)
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    "Erro",
                    string.IsNullOrWhiteSpace(result.message) ? "Falha ao carregar CPFs da coleÃ§Ã£o." : result.message,
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
                return;
            }

            var cpfs = result.Content
                .Select(g => (g?.CPF ?? string.Empty))
                .Select(raw => new string(raw.Where(char.IsDigit).ToArray()))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (cpfs.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    "Aviso",
                    "Nenhum CPF encontrado nessa coleÃ§Ã£o.",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info).ShowAsync();
                return;
            }

            var text = string.Join(" ", cpfs);
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var topLevel = Avalonia.Application.Current?.ApplicationLifetime switch
                {
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
                    Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView => Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView),
                    _ => null
                };
                if (topLevel != null)
                {
                    await topLevel.Clipboard.SetTextAsync(text);
                }
            });

            await MessageBoxManager.GetMessageBoxStandard(
                "Sucesso",
                $"{cpfs.Count} CPF(s) copiado(s) para a Ã¡rea de transferÃªncia.",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Success).ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            await MessageBoxManager.GetMessageBoxStandard(
                "Erro",
                "Erro ao copiar CPFs da coleÃ§Ã£o.",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
        }
        finally
        {
            CopyingCollectionCpfs = false;
        }
    }

    [RelayCommand]
    private async Task ManageCollectionCpfsAsync(ProfessionalTask professionalTask)
    {
        if (professionalTask == null || string.IsNullOrWhiteSpace(professionalTask.classCode))
        {
            await MessageBoxManager.GetMessageBoxStandard(
                "Erro",
                "ColeÃ§Ã£o invÃ¡lida: classCode ausente.",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
            return;
        }

        if (CopyingCollectionCpfs || ManagingCollectionCpfs)
            return;

        ManagingCollectionCpfs = true;
        try
        {
            var result = await GlobalAppStateViewModel.lfc.GetGraduatesByCPFByClassCode(professionalTask.classCode);
            if (!result.success || result.Content == null)
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    "Erro",
                    string.IsNullOrWhiteSpace(result.message) ? "Falha ao carregar CPFs da coleÃ§Ã£o." : result.message,
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
                return;
            }

            var cpfs = result.Content
                .Select(g => (g?.CPF ?? string.Empty))
                .Select(raw => new string(raw.Where(char.IsDigit).ToArray()))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (cpfs.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    "Aviso",
                    "Nenhum CPF encontrado nessa coleÃ§Ã£o.",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info).ShowAsync();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (MainWindowViewModel.Instance.SelectedTabIndex == 2)
                {
                    // JÃ¡ na aba â€“ injetar via ExecuteScriptAsync no WebView existente.
                    SearchGraduateControl.InjectCpfsIfVisible(cpfs);
                }
                else
                {
                    // Gravar CPFs no estado; o construtor do controle os consome.
                    SearchGraduateNavigationState.PendingCpfs = cpfs;
                    MainWindowViewModel.Instance.SelectedTabIndex = 2;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            await MessageBoxManager.GetMessageBoxStandard(
                "Erro",
                "Erro ao abrir gerenciamento de CPF para esta coleÃ§Ã£o.",
                MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
        }
        finally
        {
            ManagingCollectionCpfs = false;
        }
    }

    /// <summary>Aba ativa na lista de coleÃ§Ãµes: normais, vencidas ou deletadas.</summary>
    public enum CollectionsTabKind
    {
        Normal,
        Expired,
        Deleted
    }

    public enum ActiveViews
    {
        QuickAccess,
        NewsView,
        MessagesView,
        NewCollection,
        AddIds,
        EditIds,
        NewCollectionPreConfigured,
        CollectionView,
        SelectProfessional,
        CancelBilling
    }
    [ObservableProperty]
    private ActiveViews activeComponent = ActiveViews.NewsView;
    private ActiveViews lastActiveComponent;
    private ProfessionalTask? _lastSelectedCollection; // Guarda a coleÃ§Ã£o selecionada ao navegar para outras views
    partial void OnActiveComponentChanged(ActiveViews oldValue, ActiveViews newValue)
    {
        lastActiveComponent = oldValue;

        // Detectar navegaÃ§Ã£o manual para MessagesView
        if (newValue == ActiveViews.MessagesView && oldValue != ActiveViews.MessagesView)
        {
            // Guardar a turma selecionada antes de limpar, para restaurar ao voltar
            _lastSelectedCollection = SelectedCollection;
            
            // Limpar a turma selecionada quando abrir o componente de mensagens
            SelectedCollection = null;
            
            // Se nÃ£o foi a primeira verificaÃ§Ã£o inicial, Ã© navegaÃ§Ã£o manual
            if (_hasCheckedInitialMessages)
            {
                _userManuallyNavigatedToMessages = true;
            }
        }
        // Se sair de MessagesView, resetar flag de navegaÃ§Ã£o manual
        else if (oldValue == ActiveViews.MessagesView && newValue != ActiveViews.MessagesView)
        {
            _userManuallyNavigatedToMessages = false;
        }

        OnPropertyChanged(nameof(ComponentNewsViewIsVisible));
        OnPropertyChanged(nameof(ComponentNewCollectionIsVisible));
        OnPropertyChanged(nameof(ComponentAddIdsIsVisible));
        OnPropertyChanged(nameof(ComponentEditIdsIsVisible));
        OnPropertyChanged(nameof(ComponentCollectionViewIsVisible));
        OnPropertyChanged(nameof(ComponentSelectProfessionalIsIsVisible));
        OnPropertyChanged(nameof(ComponentQuickAccessIsVisible));
        OnPropertyChanged(nameof(ComponentCancelBillingIsVisible));
        OnPropertyChanged(nameof(ComponentNewCollectionPreConfiguredIsVisible));
        OnPropertyChanged(nameof(ComponentMessagesViewIsVisible));
        OnPropertyChanged(nameof(ComponentCollectionViewIsVisibleAndSafe));
    }

    /// <summary>CollectionView sÃ³ aparece quando hÃ¡ coleÃ§Ã£o selecionada (evita view quebrada se SelectedCollection virar null).</summary>
    public bool ComponentCollectionViewIsVisibleAndSafe =>
        ActiveComponent == ActiveViews.CollectionView && SelectedCollection != null;

    public bool ComponentQuickAccessIsVisible => ActiveComponent == ActiveViews.QuickAccess;
    public bool ComponentNewsViewIsVisible => ActiveComponent == ActiveViews.NewsView;
    public bool ComponentSelectProfessionalIsIsVisible => ActiveComponent == ActiveViews.SelectProfessional;
    public bool ComponentNewCollectionIsVisible => ActiveComponent == ActiveViews.NewCollection;
    public bool ComponentAddIdsIsVisible => ActiveComponent == ActiveViews.AddIds;
    public bool ComponentEditIdsIsVisible => ActiveComponent == ActiveViews.EditIds;
    public bool ComponentCollectionViewIsVisible => ActiveComponent == ActiveViews.CollectionView;
    public bool ComponentCancelBillingIsVisible => ActiveComponent == ActiveViews.CancelBilling;
    public bool ComponentNewCollectionPreConfiguredIsVisible => ActiveComponent == ActiveViews.NewCollectionPreConfigured;
    public bool ComponentMessagesViewIsVisible => ActiveComponent == ActiveViews.MessagesView;

    #endregion
    [ObservableProperty] private ObservableCollection<ProfessionalTask> collectionsList = new();
    [ObservableProperty] private ObservableCollection<ProfessionalTask> collectionsListFiltered = new();
    partial void OnCollectionsListFilteredChanged(ObservableCollection<ProfessionalTask> value) => OnPropertyChanged(nameof(VisibleCollectionsList));

    [ObservableProperty] private ObservableCollection<ProfessionalTask> expiredCollectionsListFiltered = new();
    partial void OnExpiredCollectionsListFilteredChanged(ObservableCollection<ProfessionalTask> value)
    {
        OnPropertyChanged(nameof(VisibleCollectionsList));
        NotifyExpiredCollectionsCount();
    }

    [ObservableProperty] private ObservableCollection<ProfessionalTask> deletedCollectionsListFiltered = new();
    partial void OnDeletedCollectionsListFilteredChanged(ObservableCollection<ProfessionalTask> value) => OnPropertyChanged(nameof(VisibleCollectionsList));

    /// <summary>Filtro: ID da coleÃ§Ã£o (sincronizado com a view).</summary>
    [ObservableProperty] private string filterClassCode = "";

    /// <summary>Filtro: separador (sincronizado com a view).</summary>
    [ObservableProperty] private string filterProfessionalText = "";

    private bool _isClearingFiltersForTabChange;

    partial void OnFilterClassCodeChanged(string value)
    {
        if (_isClearingFiltersForTabChange) return;
        FilterProfessionalTasks(FilterClassCode, FilterProfessionalText);
    }

    partial void OnFilterProfessionalTextChanged(string value)
    {
        if (_isClearingFiltersForTabChange) return;
        FilterProfessionalTasks(FilterClassCode, FilterProfessionalText);
    }

    /// <summary>Aba ativa na lista de coleÃ§Ãµes (normais, vencidas ou deletadas).</summary>
    [ObservableProperty] private CollectionsTabKind selectedCollectionsTab = CollectionsTabKind.Normal;
    partial void OnSelectedCollectionsTabChanged(CollectionsTabKind value)
    {
        _filterDebounceCts?.Cancel();
        IsSearchingOnServer = false;
        ShowNoResultsMessage = false;

        _isClearingFiltersForTabChange = true;
        try
        {
            filterClassCode = string.Empty;
            filterProfessionalText = string.Empty;
            OnPropertyChanged(nameof(FilterClassCode));
            OnPropertyChanged(nameof(FilterProfessionalText));
            ApplyLocalFilterAllSources(string.Empty, string.Empty);
        }
        finally
        {
            _isClearingFiltersForTabChange = false;
        }

        OnPropertyChanged(nameof(VisibleCollectionsList));
        NotifyDeletedCollectionViewState();
        OnPropertyChanged(nameof(ShowLoadMoreButtonInList));
        OnPropertyChanged(nameof(IsEnabledFiltersInList));
        OnPropertyChanged(nameof(IsListAreaLoading));
        OnPropertyChanged(nameof(IsExpiredTabActive));
        OnPropertyChanged(nameof(IsDeletedTabActive));
        OnPropertyChanged(nameof(IsNormalTabActive));
        NotifyExpiredCollectionsCount();
        ClearSelectedIfNotInCurrentFilteredVisible();
        EnsureTabDataLoadedAsync(value);
    }

    /// <summary>Lista de coleÃ§Ãµes vencidas (prazo expirado). Carregada ao abrir a aba "Vencidas" pela primeira vez.</summary>
    [ObservableProperty] private ObservableCollection<ProfessionalTask> expiredCollectionsList = new();
    partial void OnExpiredCollectionsListChanged(ObservableCollection<ProfessionalTask> value)
    {
        OnPropertyChanged(nameof(VisibleCollectionsList));
        OnPropertyChanged(nameof(IsSelectedCollectionInExpiredList));
        OnPropertyChanged(nameof(IsDeleteCollectionButtonVisible));
    }

    [ObservableProperty] private bool expiredCollectionsListIsLoading;
    partial void OnExpiredCollectionsListIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsListAreaLoading));
        NotifyExpiredCollectionsCount();
    }

    /// <summary>Lista de coleÃ§Ãµes com solicitaÃ§Ã£o de exclusÃ£o pendente (exibida na aba Deletadas).</summary>
    [ObservableProperty] private ObservableCollection<ProfessionalTask> deletedCollectionsList = new();
    partial void OnDeletedCollectionsListChanged(ObservableCollection<ProfessionalTask> value) => OnPropertyChanged(nameof(VisibleCollectionsList));

    /// <summary>Lista que a view deve exibir conforme a aba ativa (sempre a variante filtrada da sub-aba).</summary>
    public IEnumerable<ProfessionalTask> VisibleCollectionsList =>
        SelectedCollectionsTab == CollectionsTabKind.Deleted ? DeletedCollectionsListFiltered
        : SelectedCollectionsTab == CollectionsTabKind.Expired ? ExpiredCollectionsListFiltered
        : CollectionsListFiltered;

    /// <summary>True quando a aba "Deletadas" estÃ¡ ativa (para mostrar menu Restaurar nos itens).</summary>
    public bool IsDeletedTabActive => SelectedCollectionsTab == CollectionsTabKind.Deleted;
    /// <summary>True quando a aba "Vencidas" estÃ¡ ativa.</summary>
    public bool IsExpiredTabActive => SelectedCollectionsTab == CollectionsTabKind.Expired;

    /// <summary>Quantidade de coleÃ§Ãµes exibidas na aba Vencidas (lista filtrada).</summary>
    public int ExpiredCollectionsCount => ExpiredCollectionsListFiltered?.Count ?? 0;

    /// <summary>RodapÃ© com total de coleÃ§Ãµes vencidas (canto inferior esquerdo da lista).</summary>
    public bool ShowExpiredCollectionsCountFooter => IsExpiredTabActive && !IsListAreaLoading;

    void NotifyExpiredCollectionsCount()
    {
        OnPropertyChanged(nameof(ExpiredCollectionsCount));
        OnPropertyChanged(nameof(ShowExpiredCollectionsCountFooter));
    }
    /// <summary>True quando a aba "Normais" estÃ¡ ativa.</summary>
    public bool IsNormalTabActive => SelectedCollectionsTab == CollectionsTabKind.Normal;

    /// <summary>True quando a coleÃ§Ã£o selecionada pertence Ã  lista de deletadas (para mostrar botÃ£o "Cancelar deleÃ§Ã£o" no detalhe).</summary>
    public bool IsSelectedCollectionInDeletedList =>
        SelectedCollectionsTab == CollectionsTabKind.Deleted && SelectedCollection != null &&
        DeletedCollectionsList.Any(c => c?.classCode == SelectedCollection.classCode);

    /// <summary>True quando a coleÃ§Ã£o selecionada estÃ¡ na lista de vencidas (aba "Vencidas").</summary>
    public bool IsSelectedCollectionInExpiredList =>
        SelectedCollectionsTab == CollectionsTabKind.Expired && SelectedCollection != null &&
        ExpiredCollectionsList.Any(c => c?.classCode == SelectedCollection.classCode);

    void NotifyDeletedCollectionViewState()
    {
        OnPropertyChanged(nameof(IsSelectedCollectionInDeletedList));
        OnPropertyChanged(nameof(IsSelectedCollectionInExpiredList));
        OnPropertyChanged(nameof(IsDeleteCollectionButtonVisible));
        OnPropertyChanged(nameof(IsDeleteCollectionButtonEnabled));
        OnPropertyChanged(nameof(BtTagSortIsEnabledForView));
        OnPropertyChanged(nameof(BtExportIsEnabledForView));
        OnPropertyChanged(nameof(BtDownloadHdIsEnabledForView));
        OnPropertyChanged(nameof(ExpanderAdvancedIsEnabled));
    }

    /// <summary>Tag/Sort habilitado apenas quando a coleÃ§Ã£o selecionada nÃ£o Ã© da lista de deletadas.</summary>
    public bool BtTagSortIsEnabledForView => BtTagSortIsEnabled && !IsSelectedCollectionInDeletedList;
    /// <summary>Export habilitado apenas quando a coleÃ§Ã£o selecionada nÃ£o Ã© da lista de deletadas.</summary>
    public bool BtExportIsEnabledForView => BtExportIsEnabled && !IsSelectedCollectionInDeletedList;
    /// <summary>Download HD habilitado apenas quando a coleÃ§Ã£o selecionada nÃ£o Ã© da lista de deletadas.</summary>
    public bool BtDownloadHdIsEnabledForView => BtDownloadHdIsEnabled && !IsSelectedCollectionInDeletedList;
    /// <summary>AvanÃ§ado expansÃ­vel apenas quando a coleÃ§Ã£o nÃ£o estÃ¡ cancelada e nÃ£o Ã© da lista de deletadas.</summary>
    public bool ExpanderAdvancedIsEnabled => SelectedCollection?.BillingCancelled != true && !IsSelectedCollectionInDeletedList;

    [ObservableProperty] private bool deletedCollectionsListIsLoading;
    partial void OnDeletedCollectionsListIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsListAreaLoading));

    /// <summary>True quando a Ã¡rea da lista estÃ¡ em loading (conforme a aba ativa).</summary>
    public bool IsListAreaLoading =>
        SelectedCollectionsTab == CollectionsTabKind.Normal ? CollectionsListIsLoading
        : SelectedCollectionsTab == CollectionsTabKind.Expired ? ExpiredCollectionsListIsLoading
        : DeletedCollectionsListIsLoading;

    /// <summary>Mostrar botÃ£o "Carregar mais" apenas na aba de coleÃ§Ãµes normais.</summary>
    public bool ShowLoadMoreButtonInList => ShowLoadMoreButton && SelectedCollectionsTab == CollectionsTabKind.Normal;

    /// <summary>Filtros de ID / separador habilitados em todas as sub-abas (Normais, Vencidas, Lixeira).</summary>
    public bool IsEnabledFiltersInList => IsEnabledFilters;
    [ObservableProperty] private ObservableCollection<GraduateByCPF> graduatesData = new();
    [ObservableProperty] private bool updatingGraduatesData;
    [ObservableProperty] private bool copyingCollectionCpfs;
    [ObservableProperty] private bool managingCollectionCpfs;

    public bool CollectionCpfsCommandsEnabled => !CopyingCollectionCpfs && !ManagingCollectionCpfs;

    partial void OnCopyingCollectionCpfsChanged(bool value) => OnPropertyChanged(nameof(CollectionCpfsCommandsEnabled));

    partial void OnManagingCollectionCpfsChanged(bool value) => OnPropertyChanged(nameof(CollectionCpfsCommandsEnabled));
    [ObservableProperty] public bool collectionsListIsLoading = true;
    partial void OnCollectionsListIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsListAreaLoading));
    [ObservableProperty] public bool isEnabledFilters = false;
    partial void OnIsEnabledFiltersChanged(bool value) => OnPropertyChanged(nameof(IsEnabledFiltersInList));
    [ObservableProperty] public bool isSearchingOnServer = false;
    [ObservableProperty] public bool showNoResultsMessage = false;
    [ObservableProperty] public bool isUpdateProgressBars = false;
    [ObservableProperty] public int? totalPhotosForRecognition;
    [ObservableProperty] public int? totalPhotosForRecognitionDone;
    [ObservableProperty] public int? totalPhotosForAutoTreatment;
    [ObservableProperty] public int? totalPhotosForAutoTreatmentDone;
    [ObservableProperty] public bool uploadComplete = true;
    [ObservableProperty] public int? totalPhotosForOCR;
    [ObservableProperty] public int? totalPhotosForOCRDone;
    [ObservableProperty] public bool? cbUploadOnTestSystem;
    [ObservableProperty] public bool? cbAllowDeletedProductionToBeFoundAnyone;
    
    /// <summary>
    /// Indica se o combo selecionado Ã© apenas para tratamento (sem reconhecimento facial)
    /// </summary>
    [ObservableProperty] public bool isTreatmentOnlyCombo = false;

    /// <summary>
    /// Modo "Adicionar IDs" (sem reupload): mostra apenas Graduates + nome da coleÃ§Ã£o.
    /// </summary>
    [ObservableProperty] private bool isAddIdsOnlyMode = false;

    //ProfessionalTask Props
    [ObservableProperty] public string tbCollectionName;
    partial void OnTbCollectionNameChanged(string value)
    {
        ValidateCollectionName();
    }
    public const int MinCollectionIdLength = 3;
    [ObservableProperty] public bool tbCollectionNameHasError = false;
    [ObservableProperty] public bool tbCollectionNameIsEmpty = false;
    [ObservableProperty] public bool tbCollectionNameIsTooShort = false;
    [ObservableProperty] public bool tbCollectionNameHasInvalidChars = false;
    [ObservableProperty] public string tbEventFolder;
    partial void OnTbEventFolderChanged(string value)
    {
        CheckPathEventFolder();
    }
    [ObservableProperty] public string tbRecFolder;
    partial void OnTbRecFolderChanged(string value)
    {
        // Se for apenas tratamento, nÃ£o processar pasta de reconhecimentos
        if (IsTreatmentOnlyCombo)
        {
            TbRecFolderError = false;
            return;
        }

        if (Directory.Exists(value))
        {
            TbRecFolderError = false;
            InsertDataBasedOnRecFolderCommand();
        }
        else
            TbRecFolderError = true;
    }
    [ObservableProperty] public bool tbEventFolderError = false;
    [ObservableProperty] public bool tbRecFolderError = false;
    [ObservableProperty] public bool? cbHDBackup;
    
    /// <summary>
    /// Indica se o checkbox HD estÃ¡ desabilitado devido ao perÃ­odo de faturamento
    /// </summary>
    [ObservableProperty] public bool cbHDBackupIsDisabled = false;
    
    /// <summary>
    /// Mensagem de erro para exibir quando o HD nÃ£o pode ser habilitado
    /// </summary>
    [ObservableProperty] public string cbHDBackupErrorMessage = string.Empty;

    /// <summary>
    /// HD marcado e o toggle bloqueado porque hÃ¡ CPF/ID na lista (paridade com WPF CreateNewClassView.UpdateCbHdBackupIsEnabled).
    /// </summary>
    [ObservableProperty] public bool cbHDBackupLockedByCpfs = false;

    /// <summary>
    /// Habilita o checkbox HD quando nÃ£o estÃ¡ travado por faturamento nem por CPFs na lista.
    /// </summary>
    public bool CbHDBackupToggleIsEnabled => !CbHDBackupIsDisabled && !CbHDBackupLockedByCpfs;

    partial void OnCbHDBackupIsDisabledChanged(bool value) =>
        OnPropertyChanged(nameof(CbHDBackupToggleIsEnabled));

    partial void OnCbHDBackupLockedByCpfsChanged(bool value) =>
        OnPropertyChanged(nameof(CbHDBackupToggleIsEnabled));

    /// <summary>Toast: ao incluir CPF/ID em graduandos, a coleÃ§Ã£o passa a HD automaticamente.</summary>
    [ObservableProperty] public bool isGraduateHdToastVisible;

    [ObservableProperty] public string graduateHdToastMessage = string.Empty;

    private bool _graduateHdToastShownUntilCleared;
    private CancellationTokenSource? _graduateHdToastDismissCts;

    private void ShowGraduateForcesHdToast()
    {
        GraduateHdToastMessage = Loc.Tr(
            "When you add graduates with CPF/ID, the collection becomes HD automatically.",
            "Ao incluir graduandos (CPF/ID), a coleÃ§Ã£o passa a ser em HD automaticamente.");
        IsGraduateHdToastVisible = true;
        _graduateHdToastDismissCts?.Cancel();
        _graduateHdToastDismissCts = new CancellationTokenSource();
        var ct = _graduateHdToastDismissCts.Token;
        _ = DismissGraduateHdToastAfterDelayAsync(ct);
    }

    private async Task DismissGraduateHdToastAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(5000, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!ct.IsCancellationRequested)
                IsGraduateHdToastVisible = false;
        });
    }
    
    /// <summary>
    /// Indica se as opÃ§Ãµes de armazenamento HD estÃ£o visÃ­veis (quando HD estÃ¡ marcado)
    /// </summary>
    [ObservableProperty] public bool isHDStorageOptionsVisible = false;
    
    /// <summary>
    /// OpÃ§Ã£o de armazenamento HD por 3 meses
    /// </summary>
    [ObservableProperty] public bool? cbHDStorageThreeMonths = false;
    
    /// <summary>
    /// OpÃ§Ã£o de armazenamento HD por 2 anos
    /// </summary>
    [ObservableProperty] public bool? cbHDStorageTwoYears = false;
    
    /// <summary>
    /// OpÃ§Ã£o de armazenamento HD por 5 anos
    /// </summary>
    [ObservableProperty] public bool? cbHDStorageFiveYears = false;
    
    /// <summary>
    /// Data calculada para armazenamento de 3 meses
    /// </summary>
    public DateTimeOffset? HdStorageThreeMonthsDate => DateTimeOffset.Now.AddMonths(3);
    
    /// <summary>
    /// Data calculada para armazenamento de 2 anos
    /// </summary>
    public DateTimeOffset? HdStorageTwoYearsDate => DateTimeOffset.Now.AddYears(2);
    
    /// <summary>
    /// Data calculada para armazenamento de 5 anos
    /// </summary>
    public DateTimeOffset? HdStorageFiveYearsDate => DateTimeOffset.Now.AddYears(5);
    
    /// <summary>
    /// Data selecionada para armazenamento HD (baseada na opÃ§Ã£o escolhida)
    /// </summary>
    public DateTimeOffset? SelectedHdStorageDate
    {
        get
        {
            if (CbHDStorageThreeMonths == true)
                return HdStorageThreeMonthsDate;
            if (CbHDStorageTwoYears == true)
                return HdStorageTwoYearsDate;
            if (CbHDStorageFiveYears == true)
                return HdStorageFiveYearsDate;
            return null;
        }
    }
    
    /// <summary>
    /// Texto formatado da data de 3 meses
    /// </summary>
    public string HdStorageThreeMonthsDateText => HdStorageThreeMonthsDate?.ToString("dd/MM/yyyy") ?? "";
    
    /// <summary>
    /// Texto formatado da data de 2 anos
    /// </summary>
    public string HdStorageTwoYearsDateText => HdStorageTwoYearsDate?.ToString("dd/MM/yyyy") ?? "";
    
    /// <summary>
    /// Texto formatado da data de 5 anos
    /// </summary>
    public string HdStorageFiveYearsDateText => HdStorageFiveYearsDate?.ToString("dd/MM/yyyy") ?? "";
    
    /// <summary>
    /// Texto do preÃ§o para armazenamento HD selecionado
    /// </summary>
    [ObservableProperty] public string hdStoragePriceText = string.Empty;
    
    /// <summary>
    /// Indica se o texto do preÃ§o estÃ¡ visÃ­vel
    /// </summary>
    public bool IsHdStoragePriceTextVisible => !string.IsNullOrEmpty(HdStoragePriceText);
    
    partial void OnHdStoragePriceTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsHdStoragePriceTextVisible));
    }
    
    /// <summary>
    /// Indica se estÃ¡ carregando o preÃ§o
    /// </summary>
    [ObservableProperty] public bool isLoadingHDStoragePrice = false;
    
    /// <summary>
    /// Indica se pode adicionar/editar CPFs (usado para bloquear em reuploads de turmas de outro perÃ­odo)
    /// </summary>
    [ObservableProperty] public bool canAddCPFs = true;
    
    /// <summary>
    /// Mensagem de erro para exibir quando CPFs nÃ£o podem ser adicionados
    /// </summary>
    [ObservableProperty] public string cPFsErrorMessage = string.Empty;
    
    /// <summary>
    /// Indica se o checkbox AutoTreatment estÃ¡ desabilitado devido ao perÃ­odo de faturamento
    /// </summary>
    [ObservableProperty]
    public bool cbEnableAutoTreatmentIsDisabled = false;
    
    /// <summary>
    /// Indica se o checkbox "jÃ¡ estÃ£o separados" estÃ¡ desabilitado durante reupload
    /// </summary>
    [ObservableProperty]
    public bool cbUploadedPhotosAreAlreadySortedIsDisabled = false;
    
    /// <summary>
    /// Mensagem de erro para exibir quando AutoTreatment nÃ£o pode ser habilitado
    /// </summary>
    [ObservableProperty]
    public string cbEnableAutoTreatmentErrorMessage = string.Empty;

    //Buttons
    [ObservableProperty] public bool actionsButtonsIsVisible = false;
    [ObservableProperty] public bool showButtonsCollectionView = true;
    [ObservableProperty] public bool btTagSortIsEnabled = false;
    partial void OnBtTagSortIsEnabledChanged(bool value) => OnPropertyChanged(nameof(BtTagSortIsEnabledForView));
    [ObservableProperty] public bool btTagSortIsRunning = false;
    [ObservableProperty] public bool btExportIsEnabled = false;
    partial void OnBtExportIsEnabledChanged(bool value) => OnPropertyChanged(nameof(BtExportIsEnabledForView));
    [ObservableProperty] public bool btExportIsRunning = false;
    [ObservableProperty] public bool btReportsIsEnabled = false;
    [ObservableProperty] public bool btDownloadHdIsEnabled = false;
    partial void OnBtDownloadHdIsEnabledChanged(bool value) => OnPropertyChanged(nameof(BtDownloadHdIsEnabledForView));
    [ObservableProperty] public bool btDownloadHdIsRunning = false;
    [ObservableProperty] public bool btReEnqueueIsEnabled = true;
    [ObservableProperty] public bool btReenqueueIsRunning = false;
    [ObservableProperty] public bool btRequestManualTreatmentAllCollectionIsEnabled = true;
    [ObservableProperty] public bool btRequestManualTreatmentAllCollectionIsRunning = false;
    [ObservableProperty] public bool selectedCollectionIsCanceled;
    [ObservableProperty] public bool expanderAdvancedOptionsIsEnabled;
    partial void OnCbHDBackupChanged(bool? oldValue, bool? newValue)
    {
        // NÃ£o processar eventos enquanto estamos carregando dados de reupload
        if (_isLoadingReuploadData)
            return;
            
        // Mostrar/esconder opÃ§Ãµes de armazenamento quando HD Ã© marcado/desmarcado
        IsHDStorageOptionsVisible = newValue == true;
        
        // Se desmarcar HD, resetar opÃ§Ãµes de armazenamento
        if (newValue == false)
        {
            CbEnableAutoTreatment = false;
            CbHDStorageThreeMonths = false;
            CbHDStorageTwoYears = false;
            CbHDStorageFiveYears = false;
        }
        else if (newValue == true)
        {
            // Por padrÃ£o, marcar 5 anos quando HD Ã© habilitado
            CbHDStorageFiveYears = true;
            CbHDStorageThreeMonths = false;
            CbHDStorageTwoYears = false;
        }
            
        // Validar perÃ­odo de faturamento quando tentando habilitar HD
        if (newValue == true && IsReupload && SelectedCollection != null)
        {
            if (IsCollectionFromDifferentBillingPeriod(SelectedCollection))
            {
                // Desabilitar o checkbox e mostrar erro
                CbHDBackupIsDisabled = true;
                CbHDBackupErrorMessage = Loc.Tr("This collection is from another billing period, please create a new collection to perform an HD backup.");
                
                // Reverter o valor para false
                CbHDBackup = false;
                IsHDStorageOptionsVisible = false;
                
                // Bloquear adiÃ§Ã£o de CPFs quando nÃ£o Ã© HD
                CanAddCPFs = false;
                CPFsErrorMessage = Loc.Tr("This collection is from another billing period. CPFs cannot be added or modified during reupload.");
                return;
            }
        }
    }
    
    partial void OnCbHDStorageThreeMonthsChanged(bool? oldValue, bool? newValue)
    {
        if (newValue == true)
        {
            // Se marcar 3 meses, desmarcar outras opÃ§Ãµes
            CbHDStorageTwoYears = false;
            CbHDStorageFiveYears = false;
            // Carregar preÃ§o
            LoadHDStoragePriceAsync();
        }
        else
        {
            HdStoragePriceText = string.Empty;
        }
        // Notificar mudanÃ§as nas propriedades calculadas
        OnPropertyChanged(nameof(SelectedHdStorageDate));
    }
    
    partial void OnCbHDStorageTwoYearsChanged(bool? oldValue, bool? newValue)
    {
        if (newValue == true)
        {
            // Se marcar 2 anos, desmarcar outras opÃ§Ãµes
            CbHDStorageThreeMonths = false;
            CbHDStorageFiveYears = false;
            // Carregar preÃ§o
            LoadHDStoragePriceAsync();
        }
        else
        {
            HdStoragePriceText = string.Empty;
        }
        // Notificar mudanÃ§as nas propriedades calculadas
        OnPropertyChanged(nameof(SelectedHdStorageDate));
    }
    
    partial void OnCbHDStorageFiveYearsChanged(bool? oldValue, bool? newValue)
    {
        if (newValue == true)
        {
            // Se marcar 5 anos, desmarcar outras opÃ§Ãµes
            CbHDStorageThreeMonths = false;
            CbHDStorageTwoYears = false;
            // Carregar preÃ§o
            LoadHDStoragePriceAsync();
        }
        else
        {
            HdStoragePriceText = string.Empty;
        }
        // Notificar mudanÃ§as nas propriedades calculadas
        OnPropertyChanged(nameof(SelectedHdStorageDate));
    }
    
    private async void LoadHDStoragePriceAsync()
    {
        // Verifica se hÃ¡ data de armazenamento selecionada
        if (!SelectedHdStorageDate.HasValue)
        {
            HdStoragePriceText = string.Empty;
            return;
        }
        
        try
        {
            IsLoadingHDStoragePrice = true;
            HdStoragePriceText = Loc.Tr("Loading price...");
            
            if (GlobalAppStateViewModel.lfc == null)
            {
                HdStoragePriceText = Loc.Tr("Error loading price");
                return;
            }
            
            // Determina se Ã© coleÃ§Ã£o nova ou existente
            bool isNewCollection = SelectedCollection == null || string.IsNullOrEmpty(SelectedCollection.classCode);
            
            // Para coleÃ§Ã£o nova: oldScheduledDeletionDate = data de hoje, isCollectionCreation = true
            // Para coleÃ§Ã£o existente: oldScheduledDeletionDate = ScheduledDeletionDate da coleÃ§Ã£o, isCollectionCreation = false
            DateTimeOffset oldDate;
            string classCodeToUse;
            
            if (isNewCollection)
            {
                // ColeÃ§Ã£o nova: usa data de hoje como oldScheduledDeletionDate
                oldDate = DateTimeOffset.Now;
                classCodeToUse = TbCollectionName ?? "new_collection"; // Usa nome temporÃ¡rio se nÃ£o houver classCode
            }
            else
            {
                // ColeÃ§Ã£o existente: usa ScheduledDeletionDate da coleÃ§Ã£o
                oldDate = SelectedCollection.ScheduledDeletionDate ?? DateTimeOffset.Now;
                classCodeToUse = SelectedCollection.classCode;
            }
            
            // Sempre enviar newScheduledDeletionDate com 5 horas a mais (05:00 UTC em vez de 00:00)
            var newDateTimeOffset = SelectedHdStorageDate.Value.ToUniversalTime().AddHours(5);
            
            var result = await GlobalAppStateViewModel.lfc.SimulateExtendScheduledDeletionDate(
                classCodeToUse,
                oldDate.ToUniversalTime(),
                newDateTimeOffset,
                isNewCollection // isCollectionCreation
            );
            
            if (result != null && result.success == true && result.Content != null)
            {
                // Para coleÃ§Ã£o nova, usamos o preÃ§o diÃ¡rio por foto retornado pelo backend
                // Para coleÃ§Ã£o existente, usamos o valor total retornado
                if (isNewCollection)
                {
                    // Para coleÃ§Ã£o nova, o backend retorna o preÃ§o diÃ¡rio por foto em centavos,
                    // entÃ£o dividimos por 100 para exibir em reais.
                    double dailyPricePerPhotoInReais = result.Content.DailyPricePerPhoto / 100.0;
                    
                    // Calcula o preÃ§o por mÃªs (assumindo 30 dias por mÃªs)
                    int daysPerMonth = 30;
                    double monthlyPricePerPhoto = dailyPricePerPhotoInReais * daysPerMonth;
                    
                    // Formata com 4 casas decimais e substitui ponto por vÃ­rgula (formato brasileiro)
                    string monthlyPriceFormatted = monthlyPricePerPhoto.ToString("F4", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
                    HdStoragePriceText = Loc.Tr("Price per photo per month:") + $" R$ {monthlyPriceFormatted}";
                }
                else
                {
                    // Para coleÃ§Ã£o existente, o backend retorna o valor total em centavos,
                    // entÃ£o dividimos por 100 para exibir em reais.
                    double totalPrice = result.Content.TotalValue / 100.0;
                    HdStoragePriceText = Loc.Tr("Price:") + $" R$ {totalPrice:F2}";
                }
            }
            else
            {
                HdStoragePriceText = Loc.Tr("Error loading price");
            }
        }
        catch (Exception ex)
        {
            HdStoragePriceText = Loc.Tr("Error loading price");
        }
        finally
        {
            IsLoadingHDStoragePrice = false;
        }
    }
    
    [RelayCommand]
    private void SelectHDStorageThreeMonths()
    {
        CbHDStorageThreeMonths = true;
    }
    
    [RelayCommand]
    private void SelectHDStorageTwoYears()
    {
        CbHDStorageTwoYears = true;
    }
    
    [RelayCommand]
    private void SelectHDStorageFiveYears()
    {
        CbHDStorageFiveYears = true;
    }
    [ObservableProperty] public bool? cbEnableAutoTreatment;
    partial void OnCbEnableAutoTreatmentChanged(bool? oldValue, bool? newValue)
    {
        // NÃ£o processar eventos enquanto estamos carregando dados de reupload
        if (_isLoadingReuploadData)
            return;
            
        // Bloquear habilitaÃ§Ã£o de AutoTreatment em turmas de outro perÃ­odo de faturamento
        if (newValue == true && IsReupload && SelectedCollection != null)
        {
            if (IsCollectionFromDifferentBillingPeriod(SelectedCollection))
            {
                // Desabilitar o checkbox e mostrar erro
                CbEnableAutoTreatmentIsDisabled = true;
                CbEnableAutoTreatmentErrorMessage = Loc.Tr("This collection is from another billing period, please create a new collection to enable automatic enhancement.");
                
                // Reverter o valor para false
                CbEnableAutoTreatment = false;
                return;
            }
        }
        
        if (newValue == true)
        {
            CbHDBackup = true;
        }
    }
    [ObservableProperty] public bool? cbEnableAutoExclusion = true;
    [ObservableProperty] public bool? cbEnablePhotoSales;
    [ObservableProperty] public bool? cbPhotosCannotHaveWatermarks;
    [ObservableProperty] public double? tbPricePerPhotoForSellingOnline;
    [ObservableProperty] public double? tbTotalPhotosForFreePerGraduate;
    [ObservableProperty] public bool? cbAllowCPFsToSeeAllPhotos;
    partial void OnCbAllowCPFsToSeeAllPhotosChanged(bool? value)
    {
        OnPropertyChanged(nameof(IsTotalPhotosForFreePerGraduateVisible));
        if (value != true)
            CbPhotosCannotHaveWatermarks = false;
    }
    [ObservableProperty] public bool? cbUploadedPhotosAreAlreadySorted;
    [ObservableProperty] public string tbProfessionalTaskDescription;
    [ObservableProperty] public string? autoTreatmentVersion;
    [ObservableProperty] public bool isReupload = false;
    [ObservableProperty] public bool? cbOcr;

    [ObservableProperty] public bool expanderAdvancedOptions;
    [ObservableProperty] public int scrollComponentNewCollection = 0;
    [ObservableProperty] public ObservableCollection<ClassSeparationFile> classSeparationFiles = new();
    [ObservableProperty] public ClassSeparationFile selectedSeparationFile;
    partial void OnSelectedSeparationFileChanged(ClassSeparationFile value)
    {
        UpdateSeparationProgress();
        OpenSeparationFileDirectoryCommand.NotifyCanExecuteChanged();
    }

    private bool CanOpenSeparationFileDirectory =>
        SelectedSeparationFile != null
        && SelectedSeparationFile.FileLocationType == ClassSeparationFile.FileLocationTypes.LOCAL
        && !string.IsNullOrWhiteSpace(SelectedSeparationFile.FilePathInLocalDisk);

    [RelayCommand(CanExecute = nameof(CanOpenSeparationFileDirectory))]
    private void OpenSeparationFileDirectory()
    {
        if (SelectedSeparationFile?.FilePathInLocalDisk == null) return;
        try
        {
            var path = SelectedSeparationFile.FilePathInLocalDisk.Trim();
            // Abrir a pasta Save (onde estÃ¡ o separacao.hermes)
            var saveDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(saveDir) && Directory.Exists(saveDir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{saveDir}\"") { UseShellExecute = true });
                return;
            }
            // Fallback: pasta pai (ex.: .../17_03_2026__13_04_TesteEmbaralhamento)
            var parentDir = Path.GetDirectoryName(saveDir);
            if (!string.IsNullOrWhiteSpace(parentDir) && Directory.Exists(parentDir))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{parentDir}\"") { UseShellExecute = true });
        }
        catch
        {
            // best-effort: nÃ£o interromper se o Explorer falhar
        }
    }

    [ObservableProperty] public bool classSeparationFilesIsVisible;
    [ObservableProperty] public bool isCreatingCollection = false;
    [ObservableProperty] public ObservableCollection<CollectionComboOptions> dynamicCombos = new();
    partial void OnDynamicCombosChanged(ObservableCollection<CollectionComboOptions> value)
    {
        OnPropertyChanged(nameof(HasCombos));
        OnPropertyChanged(nameof(NoCombosAvailable));
    }
    
    /// <summary>
    /// Indica se hÃ¡ combos disponÃ­veis
    /// </summary>
    public bool HasCombos => DynamicCombos != null && DynamicCombos.Count > 0;
    
    /// <summary>
    /// Indica se nÃ£o hÃ¡ combos disponÃ­veis (para exibir mensagem de fallback)
    /// </summary>
    public bool NoCombosAvailable => DynamicCombos == null || DynamicCombos.Count == 0;

    /// <summary>
    /// Exibe o campo "Total de fotos grÃ¡tis por formando" quando hÃ¡ pelo menos um formando com CPF ou quando o checkbox "Permitir que as fotos sejam encontradas por qualquer pessoa" estÃ¡ marcado.
    /// </summary>
    public bool IsTotalPhotosForFreePerGraduateVisible => (CbAllowCPFsToSeeAllPhotos == true) || (GraduatesData?.Any(g => !string.IsNullOrWhiteSpace(g?.CPF)) == true);

    /// <summary>Visibilidade do campo de pasta de reconhecimentos (nÃ£o aparece em combo apenas tratamento).</summary>
    public bool RecFolderFieldIsVisible => !IsTreatmentOnlyCombo;

    partial void OnIsTreatmentOnlyComboChanged(bool value) => OnPropertyChanged(nameof(RecFolderFieldIsVisible));

    [ObservableProperty] public bool componentNewCollectionIsEnabled = true;
    [ObservableProperty] public bool loadProfessionalsIsRunning = false;



    [ObservableProperty] public List<Professional> professionals = new();
    [ObservableProperty] public Professional selectedProfessional;
    private bool isFirstProfessionalSelection = true;
    partial void OnSelectedProfessionalChanged(Professional value)
    {
        // Nï¿½o volta para a ï¿½ltima view na primeira seleï¿½ï¿½o automï¿½tica
        if (value == null)
            return;
            
        var wasViewingCollection = SelectedCollection != null && ActiveComponent == ActiveViews.CollectionView;
        
        // NÃ£o voltar quando estamos apenas abrindo a lista (LoadProfessionals define SelectedProfessional e fechava a lista no 1Âº clique)
        if (!isFirstProfessionalSelection && !wasViewingCollection && !_isOpeningSelectProfessionalView)
        {
            BackLasViewCommand();
        }
        isFirstProfessionalSelection = false;
        
        // SÃ³ atualizar e salvar o separador quando o usuÃ¡rio escolher um profissional DIFERENTE do atual da coleÃ§Ã£o
        // (evita salvar ao abrir a lista, quando LoadProfessionals define SelectedProfessional = Professionals[0])
        if (SelectedCollection != null && value.username != SelectedCollection.professionalLogin)
        {
            SelectedCollection.professionalLogin = SelectedProfessional.username;
            _ = SaveCollectionSeparatorChange();
        }
        CurrentProfessionalName = SelectedProfessional.username;
        
        // SÃ³ voltar para a view da coleÃ§Ã£o se o usuÃ¡rio realmente escolheu um separador (nÃ£o quando estamos apenas abrindo a lista)
        if (wasViewingCollection && !_isOpeningSelectProfessionalView)
        {
            ActiveComponent = ActiveViews.CollectionView;
        }
    }
    
    private async Task SaveCollectionSeparatorChange()
    {
        if (SelectedCollection == null || GlobalAppStateViewModel.lfc == null)
            return;
            
        IsChangingSeparator = true;
        try
        {
            // Criar um ProfessionalTask apenas com as informaÃ§Ãµes necessÃ¡rias para atualizar o separador
            // CORREÃ‡ÃƒO: Normalizar os paths removendo barras finais para evitar duplicaÃ§Ã£o no backend
            ProfessionalTask pt = new ProfessionalTask()
            {
                professionalLogin = SelectedProfessional.username,
                classCode = SelectedCollection.classCode,
                companyUsername = SelectedCollection.companyUsername,
                // Manter os valores atuais da coleÃ§Ã£o (normalizados)
                originalClassFolder = SelectedCollection.originalClassFolder?.TrimEnd('\\', '/'),
                originalEventsFolder = SelectedCollection.originalEventsFolder?.TrimEnd('\\', '/'),
                originalRecFolder = SelectedCollection.originalRecFolder?.TrimEnd('\\', '/'),
                UploadOnTestSystem = SelectedCollection.UploadOnTestSystem ?? false,
                EnableFaceRelevanceDetection = SelectedCollection.EnableFaceRelevanceDetection,
                AutoTreatment = SelectedCollection.AutoTreatment,
                UploadPhotosAreAlreadySorted = SelectedCollection.UploadPhotosAreAlreadySorted,
                AllowCPFsToSeeAllPhotos = SelectedCollection.AllowCPFsToSeeAllPhotos,
                UploadHD = SelectedCollection.UploadHD,
                UploadComplete = SelectedCollection.UploadComplete,
                Description = SelectedCollection.Description,
                EnablePhotosSales = SelectedCollection.EnablePhotosSales,
                PhotosCannotHaveWatermarks = SelectedCollection.PhotosCannotHaveWatermarks,
                PricePerPhotoForSellingOnlineInCents = SelectedCollection.PricePerPhotoForSellingOnlineInCents,
                TotalPhotosForFreePerGraduate = SelectedCollection.TotalPhotosForFreePerGraduate,
                OCR = SelectedCollection.OCR,
                AllowDeletedProductionToBeFoundAnyone = SelectedCollection.AllowDeletedProductionToBeFoundAnyone,
            };
            
            // Salvar o classCode antes de atualizar a lista (para evitar NullReferenceException)
            var currentClassCode = SelectedCollection.classCode;
            
            // Atualizar apenas o separador (sem alterar as fotos)
            var r = await GlobalAppStateViewModel.lfc.UpdateOrCreateProfessionalTaskAsync(pt, new List<string>(), new List<string>());
            if (r != null && r.success)
            {
                // Atualizar apenas a PT especÃ­fica (nÃ£o recarregar toda a lista de coleÃ§Ãµes)
                var updatedCollection = await GlobalAppStateViewModel.lfc.GetProfessionalTask(currentClassCode);
                if (updatedCollection != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateCollectionInList(updatedCollection, currentClassCode);
                        var collectionInList = CollectionsList.FirstOrDefault(c => c.classCode == currentClassCode);
                        if (collectionInList != null)
                        {
                            if (SelectedCollection != collectionInList)
                            {
                                _isUpdatingSelectedCollection = true;
                                SelectedCollection = collectionInList;
                                _isUpdatingSelectedCollection = false;
                            }
                            else
                            {
                                OnPropertyChanged(nameof(SelectedCollection));
                            }
                        }
                    });
                }
            }
            else
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(r?.message ?? "Erro ao atualizar o separador da coleÃ§Ã£o.");
            }
        }
        catch (Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk($"Erro ao salvar alteraÃ§Ã£o do separador: {ex.Message}");
        }
        finally
        {
            IsChangingSeparator = false;
        }
    }
    
    [ObservableProperty] public string currentProfessionalName;
    [ObservableProperty] public bool isChangingSeparator = false;

    //FREE TRIAL PROPERTIES
    //[ObservableProperty] public bool accountInPeriodFreeTrial = false;
    //[ObservableProperty] public string? freePhotosRemainingInTrialPeriod;
    //[ObservableProperty] public double currentPercentagePhotosFreeTrialRemaining;
    //[ObservableProperty] public string freeTrialExpiryDate = "N/A";
    //[ObservableProperty] public bool isFirstUse = false;
    [ObservableProperty] public RemainingFreeTrialPhotosResult remainingFreeTrialPhotosResult = new RemainingFreeTrialPhotosResult { IsFreeTrialActive = false };


    //PPP
    [ObservableProperty] public bool resumeSeparationFileComponentIsVisible;
    [ObservableProperty] public SeparationProgress? separationProgressValue;
    [ObservableProperty] public string labelPercentNotSeparated;
    [ObservableProperty] public string labelPercentSeparated;
    [ObservableProperty] public double separatedRectHeight;
    [ObservableProperty] public double notSeparatedRectHeight;
    [ObservableProperty] public ServerProgress? serverProgressValues;

    public ObservableCollection<string> BlockTypeOptions { get; } =
    new ObservableCollection<string> { "WATERMARK", "ACCESS_DENIED" };

    [ObservableProperty] private AddIdsViewModel? addIdsVm;
    [ObservableProperty] private EditIdsViewModel? editIdsVm;

    [ObservableProperty] public ProfessionalTask selectedCollectionForCancelBilling;
    [ObservableProperty] public bool cancelBillingIsRunning;

    [ObservableProperty] public bool reportsContainerIsVisible = false;

    [ObservableProperty] public bool generatingGeneralReport = false;
    [ObservableProperty] public bool generatingReportPerGraduate = false;

    // Add New Professional Properties
    [ObservableProperty] public string tbNewProfessionalUsername = "";
    [ObservableProperty] public string tbNewProfessionalEmail = "";
    [ObservableProperty] public string tbNewProfessionalConfirmEmail = "";
    [ObservableProperty] public bool createProfessionalIsRunning = false;
    [ObservableProperty] public string createProfessionalErrorMessage = "";
    [ObservableProperty] public string createProfessionalSuccessMessage = "";

    // Load More Button Properties
    [ObservableProperty] public bool showLoadMoreButton = true;
    partial void OnShowLoadMoreButtonChanged(bool value) => OnPropertyChanged(nameof(ShowLoadMoreButtonInList));
    [ObservableProperty] public bool isLoadingMoreTasks = false;
    partial void OnIsLoadingMoreTasksChanged(bool value)
    {
        // Atualiza o texto do botÃ£o quando o estado de carregamento muda
        UpdateLoadOldButtonText();
    }
    [ObservableProperty] public bool hasLoadedAllTasks = false;
    
    // CORREÃ‡ÃƒO: Propriedade para texto dinÃ¢mico do botÃ£o "Load old"
    [ObservableProperty] public string loadOldButtonText = "";


    public CollectionsViewModel()
    {
        Instance = this;
        LoadProfessionalTasks();
        
        // CORREÃ‡ÃƒO: Inicializa o texto do botÃ£o e escuta mudanÃ§as de idioma
        UpdateLoadOldButtonText();
        App.LanguageChanged += OnLanguageChanged;
        ThemeManager.ThemeApplied += OnApplicationThemeApplied;
        // Tema jÃ¡ pode ter sido aplicado antes do VM existir â€” alinha conversores das abas ao tema atual
        Dispatcher.UIThread.Post(() => OnApplicationThemeApplied(this, EventArgs.Empty), DispatcherPriority.Loaded);
        Task.Run(() => LoadProfessionals());
        GetInfosAboutFreeTrialPeriod();
        LoadDynamicCombos();
        
        GraduatesData.CollectionChanged += GraduatesData_CollectionChanged;
        
        // Observar mudanÃ§as nas mensagens nÃ£o lidas para decidir view inicial
        InitializeMessagesViewLogic();
        
        _timerServerProgress = new System.Timers.Timer();
        _timerServerProgress.AutoReset = false;
        _timerServerProgress.Elapsed += (_, _) =>
        {
            if (ActiveComponent != ActiveViews.CollectionView || SelectedCollection == null) return;
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await RefreshServerProgressAndRelatedUI();
                RefreshServerProgressPollingState();
            });
        };

        _timerSeparationProgress = new System.Timers.Timer();
        _timerSeparationProgress.AutoReset = false;
        _timerSeparationProgress.Elapsed += (_, _) =>
        {
            if (ActiveComponent != ActiveViews.CollectionView || SelectedCollection == null) return;
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await UpdateSeparationProgress();
                RefreshSeparationProgressPollingState();
            });
        };

        RefreshServerProgressPollingState();
    }

    private readonly HashSet<INotifyPropertyChanged> _graduatePropertyChangedSubscriptions = new();

    private void GraduatesData_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsTotalPhotosForFreePerGraduateVisible));
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var inpc in _graduatePropertyChangedSubscriptions)
                inpc.PropertyChanged -= GraduateByCPF_PropertyChanged;
            _graduatePropertyChangedSubscriptions.Clear();
            UpdateHdBackupStateFromGraduatesData();
            return;
        }
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.Cast<GraduateByCPF>())
            {
                if (item is INotifyPropertyChanged inpc)
                {
                    inpc.PropertyChanged += GraduateByCPF_PropertyChanged;
                    _graduatePropertyChangedSubscriptions.Add(inpc);
                }
            }
        }
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.Cast<GraduateByCPF>())
            {
                if (item is INotifyPropertyChanged inpc)
                {
                    inpc.PropertyChanged -= GraduateByCPF_PropertyChanged;
                    _graduatePropertyChangedSubscriptions.Remove(inpc);
                }
            }
        }
        UpdateHdBackupStateFromGraduatesData();
    }

    private void GraduateByCPF_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraduateByCPF.CPF))
        {
            OnPropertyChanged(nameof(IsTotalPhotosForFreePerGraduateVisible));
            UpdateHdBackupStateFromGraduatesData();
        }
    }

    /// <summary>
    /// Com CPF/ID na lista, o backup HD fica obrigatÃ³rio e o toggle Ã© desabilitado (igual ao dashboard WPF).
    /// </summary>
    private void UpdateHdBackupStateFromGraduatesData()
    {
        var hasCpfs = GraduatesData != null &&
                      GraduatesData.Any(static g => g != null && !string.IsNullOrWhiteSpace(g.CPF));
        CbHDBackupLockedByCpfs = hasCpfs;
        if (hasCpfs && CbHDBackup != true)
            CbHDBackup = true;

        if (hasCpfs && !_graduateHdToastShownUntilCleared)
        {
            _graduateHdToastShownUntilCleared = true;
            ShowGraduateForcesHdToast();
        }
        else if (!hasCpfs)
            _graduateHdToastShownUntilCleared = false;
    }

    private bool _hasCheckedInitialMessages = false;
    private bool _userManuallyNavigatedToMessages = false;

    // --- Timer: progresso no servidor (rec, OCR, tratamento) ---
    private System.Timers.Timer? _timerServerProgress;
    private const int ServerProgressPollingIntervalWhenLoadingMinMs = 10000;
    private const int ServerProgressPollingIntervalWhenLoadingMaxMs = 420000;
    private const int ServerProgressPollingIntervalFallbackMs = 60000;

    // --- Timer: tagadas/nÃ£o tagadas (arquivo de separaÃ§Ã£o) ---
    private System.Timers.Timer? _timerSeparationProgress;
    private const int SeparationProgressPollingIntervalMinMs = 60000;   // 1 min
    private const int SeparationProgressPollingIntervalMaxMs = 600000;   // 10 min

    /// <summary>Indica se rec + OCR + tratamento estÃ£o em 100%. Se true, nÃ£o faz polling; sÃ³ atualiza ao clicar na coleÃ§Ã£o.</summary>
    private bool IsServerProgressFullyComplete()
    {
        if (ServerProgressValues == null || SelectedCollection == null) return false;
        int total = ServerProgressValues.total ?? 0;
        if (total == 0) return true;
        bool recognitionComplete = (ServerProgressValues.done >= total);
        bool ocrComplete = !(SelectedCollection.OCR == true) || (ServerProgressValues.ocr >= total);
        bool autoTreatmentComplete = !(SelectedCollection.AutoTreatment == true) || (ServerProgressValues.autoTreated >= total);
        return recognitionComplete && ocrComplete && autoTreatmentComplete;
    }

    /// <summary>Indica se todas as fotos do arquivo de separaÃ§Ã£o estÃ£o tagadas. Se true, nÃ£o faz polling; sÃ³ atualiza ao clicar.</summary>
    private bool IsSeparationProgressFullyTagged()
    {
        if (SelectedSeparationFile == null || SeparationProgressValue == null) return true;
        if (SeparationProgressValue.totalPhotos == 0) return true;
        return SeparationProgressValue.emptyPhotos == 0;
    }

    /// <summary>Intervalo para polling do servidor (rec/OCR/tratamento). SÃ³ usado quando nÃ£o estÃ¡ 100%.</summary>
    private double GetServerProgressPollingIntervalMs()
    {
        if (SelectedCollection == null) return ServerProgressPollingIntervalFallbackMs;
        var totalPhotos = (SelectedCollection.eventPhotos ?? 0) + (SelectedCollection.recognitionPhotos ?? 0);
        return Math.Min(Math.Max(ServerProgressPollingIntervalWhenLoadingMinMs, totalPhotos * 50), ServerProgressPollingIntervalWhenLoadingMaxMs);
    }

    /// <summary>Intervalo para polling de tagadas/nÃ£o tagadas. DinÃ¢mico entre 1 min e 10 min.</summary>
    private double GetSeparationProgressPollingIntervalMs()
    {
        if (SeparationProgressValue == null || SeparationProgressValue.totalPhotos <= 0)
            return SeparationProgressPollingIntervalMinMs;
        var total = SeparationProgressValue.totalPhotos;
        return Math.Min(SeparationProgressPollingIntervalMaxMs, Math.Max(SeparationProgressPollingIntervalMinMs, total * 150));
    }

    /// <summary>Atualiza apenas progresso do servidor (rec, OCR, tratamento) e UI que depende dele. Usado pelo timer e ao clicar na coleÃ§Ã£o.</summary>
    private async Task RefreshServerProgressAndRelatedUI()
    {
        if (SelectedCollection == null) return;
        try
        {
            IsUpdateProgressBars = true;
            ActionsButtonsIsVisible = false;
            await UpdateProgressBars();
            if (SelectedCollection != null)
                await UpdateClassSeparationFile(SelectedCollection.classCode);
            SelectedCollectionIsCanceled = SelectedCollection?.BillingCancelled == true;
            UploadComplete = SelectedCollection?.UploadComplete == true;
            if (ServerProgressValues != null)
                BtTagSortIsEnabled = ServerProgressValues.done >= ServerProgressValues.total;
            if (ServerProgressValues?.done >= ServerProgressValues?.total == true)
                ActionsButtonsIsVisible = true;
            BtExportIsEnabled = SelectedSeparationFile != null;
            BtDownloadHdIsEnabled = SelectedCollection?.UploadHD == true;
            if (SelectedCollection?.BillingCancelled == true)
            {
                BtTagSortIsEnabled = false;
                BtExportIsEnabled = false;
                BtReportsIsEnabled = false;
                BtDownloadHdIsEnabled = false;
            }
        }
        finally
        {
            IsUpdateProgressBars = false;
        }
    }

    /// <summary>Liga/desliga o timer de progresso do servidor: desliga se 100% ou sem coleÃ§Ã£o; senÃ£o agenda prÃ³ximo disparo.</summary>
    private void RefreshServerProgressPollingState()
    {
        _timerServerProgress?.Stop();
        if (ActiveComponent != ActiveViews.CollectionView || SelectedCollection == null) return;
        if (IsServerProgressFullyComplete()) return;
        _timerServerProgress!.Interval = GetServerProgressPollingIntervalMs();
        _timerServerProgress.Start();
    }

    /// <summary>Liga/desliga o timer de tagadas/nÃ£o tagadas: desliga se 100% tagadas ou sem arquivo; senÃ£o agenda prÃ³ximo disparo (1â€“10 min).</summary>
    private void RefreshSeparationProgressPollingState()
    {
        _timerSeparationProgress?.Stop();
        if (ActiveComponent != ActiveViews.CollectionView || SelectedCollection == null || SelectedSeparationFile == null) return;
        if (IsSeparationProgressFullyTagged()) return;
        _timerSeparationProgress!.Interval = GetSeparationProgressPollingIntervalMs();
        _timerSeparationProgress.Start();
    }
    
    /// <summary>
    /// Inicializa a lÃ³gica de observaÃ§Ã£o de mensagens nÃ£o lidas para decidir qual view mostrar
    /// </summary>
    private void InitializeMessagesViewLogic()
    {
        // Aguardar um pouco para garantir que o MainWindowViewModel foi inicializado e carregou as mensagens
        Task.Run(async () =>
        {
            // Aguardar atÃ© que o MainWindowViewModel esteja disponÃ­vel
            MainWindowViewModel? mainVM = null;
            int attempts = 0;
            while (mainVM == null && attempts < 50) // MÃ¡ximo 5 segundos
            {
                await Task.Delay(100);
                mainVM = MainWindowViewModel.Instance;
                attempts++;
            }
            
            if (mainVM == null)
            {
                Console.WriteLine("CollectionsViewModel: MainWindowViewModel nÃ£o encontrado apÃ³s inicializaÃ§Ã£o");
                return;
            }
            
            // Aguardar atÃ© que as mensagens sejam carregadas da API (ou timeout)
            // Isso garante que a decisÃ£o de qual view mostrar seja baseada na resposta real da API
            int maxWaitAttempts = 100; // MÃ¡ximo 10 segundos (100 * 100ms)
            int waitAttempts = 0;
            bool messagesLoaded = false;
            
            while (!messagesLoaded && waitAttempts < maxWaitAttempts)
            {
                await Task.Delay(100);
                waitAttempts++;
                
                // Verificar se as mensagens foram carregadas
                try
                {
                    messagesLoaded = mainVM.MessagesLoaded;
                }
                catch
                {
                    // Se houver erro ao verificar, continuar aguardando
                }
            }
            
            if (!messagesLoaded)
            {
                Console.WriteLine("CollectionsViewModel: Timeout aguardando carregamento de mensagens. Usando estado padrÃ£o (welcome).");
            }
            
            // Verificar estado inicial das mensagens apenas uma vez na inicializaÃ§Ã£o
            // Agora que as mensagens foram carregadas (ou timeout), podemos decidir qual view mostrar
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_hasCheckedInitialMessages)
                {
                    CheckAndUpdateInitialView(mainVM, isInitialCheck: true);
                    _hasCheckedInitialMessages = true;
                }
            });
            
            // Observar mudanÃ§as futuras em ShouldShowMessagesOnStartup usando PropertyChanged
            // Criar um observador que verifica quando a propriedade muda
            System.Timers.Timer messagesCheckTimer = new System.Timers.Timer();
            messagesCheckTimer.Interval = 2000; // Verificar a cada 2 segundos (menos agressivo)
            bool lastState = mainVM.ShouldShowMessagesOnStartup;
            messagesCheckTimer.Elapsed += (s, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (mainVM.ShouldShowMessagesOnStartup != lastState)
                    {
                        lastState = mainVM.ShouldShowMessagesOnStartup;
                        CheckAndUpdateInitialView(mainVM, isInitialCheck: false);
                    }
                }, DispatcherPriority.Background);
            };
            messagesCheckTimer.Start();
        });
    }
    
    /// <summary>
    /// Verifica se deve mostrar a view de mensagens ou welcome baseado em mensagens nÃ£o lidas
    /// </summary>
    private void CheckAndUpdateInitialView(MainWindowViewModel mainVM, bool isInitialCheck)
    {
        try
        {
            // Verificar se mainVM Ã© vÃ¡lido
            if (mainVM == null)
            {
                Console.WriteLine("CollectionsViewModel: MainWindowViewModel Ã© null ao verificar view inicial");
                // Em caso de erro, garantir que mostra welcome (padrÃ£o seguro)
                if (isInitialCheck && ActiveComponent != ActiveViews.NewsView)
                {
                    ActiveComponent = ActiveViews.NewsView;
                }
                return;
            }

            if (isInitialCheck)
            {
                // Na inicializaÃ§Ã£o: se houver mensagens nÃ£o lidas, mostrar mensagens
                // Se houver erro (ShouldShowMessagesOnStartup retorna false), mostrar welcome
                try
                {
                    if (mainVM.ShouldShowMessagesOnStartup)
                    {
                        ActiveComponent = ActiveViews.MessagesView;
                    }
                    else
                    {
                        // Se nÃ£o houver mensagens ou houver erro, garantir que mostra welcome
                        ActiveComponent = ActiveViews.NewsView;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CollectionsViewModel: Erro ao verificar ShouldShowMessagesOnStartup: {ex.Message}");
                    // Em caso de erro, mostrar welcome por padrÃ£o
                    ActiveComponent = ActiveViews.NewsView;
                }
            }
            else
            {
                // ApÃ³s inicializaÃ§Ã£o: se todas as mensagens foram lidas e estamos em MessagesView,
                // voltar para NewsView (mas sÃ³ se nÃ£o foi navegaÃ§Ã£o manual)
                try
                {
                    if (!mainVM.ShouldShowMessagesOnStartup && ActiveComponent == ActiveViews.MessagesView && !_userManuallyNavigatedToMessages)
                    {
                        ActiveComponent = ActiveViews.NewsView;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CollectionsViewModel: Erro ao verificar mudanÃ§a de estado: {ex.Message}");
                    // Em caso de erro, nÃ£o fazer nada (manter estado atual)
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CollectionsViewModel: Erro ao verificar view inicial: {ex.Message}");
            // Em caso de erro geral, garantir que mostra welcome (padrÃ£o seguro)
            if (isInitialCheck)
            {
                try
                {
                    ActiveComponent = ActiveViews.NewsView;
                }
                catch
                {
                    // Se nem isso funcionar, pelo menos logar
                    Console.WriteLine("CollectionsViewModel: Erro crÃ­tico ao definir view inicial");
                }
            }
        }
    }
    
    /// <summary>
    /// MÃ©todo chamado quando o idioma Ã© alterado
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        try
        {
            // CORREÃ‡ÃƒO: Atualiza o texto do botÃ£o "Load old" quando o idioma muda
            UpdateLoadOldButtonText();
            
            // Recarregar combos com o novo idioma (sem await para evitar erro)
            Task.Run(() => LoadDynamicCombos());
        }
        catch (Exception ex)
        {
            // Log error silently
        }
    }
    
    // CORREÃ‡ÃƒO: MÃ©todo para atualizar o texto do botÃ£o "Load old"
    private void UpdateLoadOldButtonText()
    {
        LoadOldButtonText = IsLoadingMoreTasks ? Loc.Tr("Loading...") : Loc.Tr("Load old");
    }
    
    /// <summary>
    /// Descarrega os eventos quando o ViewModel Ã© destruÃ­do
    /// </summary>
    ~CollectionsViewModel()
    {
        App.LanguageChanged -= OnLanguageChanged;
        ThemeManager.ThemeApplied -= OnApplicationThemeApplied;
    }

    /// <summary>Conversores das sub-abas de coleÃ§Ãµes leem ThemeDictionaries via ActualThemeVariant; forÃ§a rebind ao mudar dark/light.</summary>
    private void OnApplicationThemeApplied(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsNormalTabActive));
        OnPropertyChanged(nameof(IsExpiredTabActive));
        OnPropertyChanged(nameof(IsDeletedTabActive));
    }

    public async Task LoadProfessionalTasks()
    {
        try
        {
            CollectionsListIsLoading = true;
            IsEnabledFilters = false;
            
            var r = await GlobalAppStateViewModel.lfc.getCompanyProfessionalTasks();

            if (r != null)
            {
                CollectionsList = new ObservableCollection<ProfessionalTask>(r);
                ApplyLocalFilterAllSources(FilterClassCode ?? string.Empty, FilterProfessionalText ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            CollectionsListIsLoading = false;
            IsEnabledFilters = true;
        }
    }

    public async Task UpdateProfessionalTasksList(ProfessionalTask pt = null)
    {
        try
        {
            CollectionsListIsLoading = true;
            IsEnabledFilters = false;
            
            // CORREÃ‡ÃƒO: Salvar o classCode da coleÃ§Ã£o selecionada antes de atualizar a lista
            string? selectedClassCode = SelectedCollection?.classCode;
            
            {
                if(pt != null)
                {
                    CollectionsList.Insert(0, pt);
                    ApplyLocalFilterAllSources(FilterClassCode ?? string.Empty, FilterProfessionalText ?? string.Empty);
                }
                else
                {
                    var r = await GlobalAppStateViewModel.lfc.getCompanyProfessionalTasks();
                    if (r != null)
                    {
                        CollectionsList = new ObservableCollection<ProfessionalTask>(r);
                        ApplyLocalFilterAllSources(FilterClassCode ?? string.Empty, FilterProfessionalText ?? string.Empty);
                    }
                }
            }
            
            // CORREÃ‡ÃƒO: Se havia uma coleÃ§Ã£o selecionada, verificar se ela ainda existe na lista
            // Se nÃ£o existir mais (foi deletada), limpar a seleÃ§Ã£o (vai voltar para home automaticamente)
            if (!string.IsNullOrEmpty(selectedClassCode))
            {
                var collectionStillExists = CollectionsList.Any(c => c.classCode == selectedClassCode);
                if (!collectionStillExists && SelectedCollection != null)
                {
                    SelectedCollection = null;
                }
            }

        }
        catch
        {

        }
        finally
        {
            CollectionsListIsLoading = false;
            IsEnabledFilters = true;
        }
    }

    public async Task LoadAllTasksFromLastFiveYears()
    {
        try
        {
            IsLoadingMoreTasks = true;
            ShowLoadMoreButton = false;
            CollectionsListIsLoading = true; // Mostrar loading geral (esconde a lista e mostra skeleton)
            
            int daysFilterLimit = 365 * 5; // 5 anos
            var pts = await GlobalAppStateViewModel.lfc.getCompanyProfessionalTasks(daysFilterLimit);

            if (pts != null && pts.Count > 0)
            {
                // Carregar todas as coleÃ§Ãµes em memÃ³ria primeiro (sem atualizar UI)
                var newCollections = new List<ProfessionalTask>();
                foreach (var pt in pts)
                {
                    // Verificar se o item jÃ¡ existe na lista para evitar duplicatas
                    if (!CollectionsList.Any(existing => existing.classCode == pt.classCode))
                    {
                        newCollections.Add(pt);
                    }
                }
                
                // Adicionar todas as novas coleÃ§Ãµes de uma vez na UI (evita piscar)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var pt in newCollections)
                    {
                        CollectionsList.Add(pt);
                    }
                    
                    // Reaplicar o filtro atual (vazio por padrÃ£o, mas mantÃ©m qualquer filtro que o usuÃ¡rio tenha aplicado)
                    // O filtro serÃ¡ reaplicado automaticamente quando CollectionsListIsLoading for false
                    // e a view for atualizada
                });
            }
            
            HasLoadedAllTasks = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar tarefas dos Ãºltimos 5 anos: {ex.Message}");
            ShowLoadMoreButton = true; // Mostrar o botÃ£o novamente em caso de erro
        }
        finally
        {
            IsLoadingMoreTasks = false;
            CollectionsListIsLoading = false; // Esconder loading geral (mostra a lista atualizada)
            
            // Reaplicar o filtro apÃ³s o loading terminar para atualizar a lista filtrada
            // Isso garante que as novas coleÃ§Ãµes apareÃ§am na lista filtrada
            FilterProfessionalTasks("", "");
        }
    }

    private enum CollectionSearchCategory
    {
        Normal,
        StorageExpired,
        PendingDeletion
    }

    private static CollectionSearchCategory GetCollectionSearchCategory(ProfessionalTask pt)
    {
        if (pt == null) return CollectionSearchCategory.Normal;
        if (pt.DeletionRequested == true || pt.EnqueuedForDeletion == true)
            return CollectionSearchCategory.PendingDeletion;
        if (pt.ScheduledDeletionDate.HasValue && pt.ScheduledDeletionDate.Value <= DateTimeOffset.Now)
            return CollectionSearchCategory.StorageExpired;
        return CollectionSearchCategory.Normal;
    }

    private static CollectionsTabKind TabForSearchCategory(CollectionSearchCategory category) =>
        category switch
        {
            CollectionSearchCategory.PendingDeletion => CollectionsTabKind.Deleted,
            CollectionSearchCategory.StorageExpired => CollectionsTabKind.Expired,
            _ => CollectionsTabKind.Normal
        };

    private bool FilterTextsMatch(string classCode, string professional) =>
        string.Equals(FilterClassCode ?? string.Empty, classCode, StringComparison.Ordinal) &&
        string.Equals(FilterProfessionalText ?? string.Empty, professional, StringComparison.Ordinal);

    /// <summary>
    /// Aplica o filtro de ID / separador sobre as trÃªs fontes e atualiza as listas filtradas de cada sub-aba.
    /// </summary>
    private void ApplyLocalFilterAllSources(string classCode, string professional)
    {
        static bool Match(ProfessionalTask task, string cc, string sep)
        {
            var login = task.professionalLogin ?? string.Empty;
            var taskClass = task.classCode ?? string.Empty;
            bool matchClass = string.IsNullOrEmpty(cc) ||
                              taskClass.Contains(cc, StringComparison.OrdinalIgnoreCase);
            bool matchProfessional = string.IsNullOrEmpty(sep) ||
                                     login.Contains(sep, StringComparison.OrdinalIgnoreCase);
            return matchClass && matchProfessional;
        }

        if (CollectionsList != null)
        {
            var filtered = CollectionsList.Where(t => Match(t, classCode, professional)).ToList();
            CollectionsListFiltered = new ObservableCollection<ProfessionalTask>(filtered);
        }
        else
            CollectionsListFiltered = new ObservableCollection<ProfessionalTask>();

        var filteredEx = ExpiredCollectionsList.Where(t => Match(t, classCode, professional)).ToList();
        ExpiredCollectionsListFiltered = new ObservableCollection<ProfessionalTask>(filteredEx);

        var filteredDel = DeletedCollectionsList.Where(t => Match(t, classCode, professional)).ToList();
        DeletedCollectionsListFiltered = new ObservableCollection<ProfessionalTask>(filteredDel);
    }

    private void MergeServerFetchedTaskForCategory(ProfessionalTask pt, CollectionSearchCategory category)
    {
        if (pt == null || string.IsNullOrEmpty(pt.classCode)) return;
        switch (category)
        {
            case CollectionSearchCategory.PendingDeletion:
                if (!DeletedCollectionsList.Any(c => c.classCode == pt.classCode))
                    DeletedCollectionsList.Insert(0, pt);
                break;
            case CollectionSearchCategory.StorageExpired:
                if (!ExpiredCollectionsList.Any(c => c.classCode == pt.classCode))
                    ExpiredCollectionsList.Insert(0, pt);
                break;
            default:
                if (CollectionsList == null) return;
                if (!CollectionsList.Any(c => c.classCode == pt.classCode))
                    CollectionsList.Add(pt);
                break;
        }
    }

    private void ClearSelectedIfNotInCurrentFilteredVisible()
    {
        if (SelectedCollection == null) return;
        var ok = SelectedCollectionsTab switch
        {
            CollectionsTabKind.Normal => CollectionsListFiltered.Contains(SelectedCollection),
            CollectionsTabKind.Expired => ExpiredCollectionsListFiltered.Contains(SelectedCollection),
            CollectionsTabKind.Deleted => DeletedCollectionsListFiltered.Contains(SelectedCollection),
            _ => false
        };
        if (!ok)
            SelectedCollection = null;
    }

    /// <summary>
    /// Filtra as coleÃ§Ãµes na lista local e, se nÃ£o encontrar resultados na sub-aba ativa, busca no servidor.
    /// </summary>
    public void FilterProfessionalTasks(string? classCode, string? professional)
    {
        _filterDebounceCts?.Cancel();
        _filterDebounceCts = new CancellationTokenSource();
        var token = _filterDebounceCts.Token;

        var searchClassCode = classCode ?? string.Empty;
        var searchProfessional = professional ?? string.Empty;

        if (!FilterTextsMatch(searchClassCode, searchProfessional))
        {
            _isClearingFiltersForTabChange = true;
            try
            {
                filterClassCode = searchClassCode;
                filterProfessionalText = searchProfessional;
                OnPropertyChanged(nameof(FilterClassCode));
                OnPropertyChanged(nameof(FilterProfessionalText));
            }
            finally
            {
                _isClearingFiltersForTabChange = false;
            }
        }

        ShowNoResultsMessage = false;

        if (CollectionsList == null && SelectedCollectionsTab == CollectionsTabKind.Normal)
        {
            CollectionsListFiltered = new ObservableCollection<ProfessionalTask>();
            ExpiredCollectionsListFiltered = new ObservableCollection<ProfessionalTask>();
            DeletedCollectionsListFiltered = new ObservableCollection<ProfessionalTask>();
            if (SelectedCollection != null)
                SelectedCollection = null;
            return;
        }

        ApplyLocalFilterAllSources(searchClassCode, searchProfessional);

        var activeCount = SelectedCollectionsTab switch
        {
            CollectionsTabKind.Normal => CollectionsListFiltered.Count,
            CollectionsTabKind.Expired => ExpiredCollectionsListFiltered.Count,
            CollectionsTabKind.Deleted => DeletedCollectionsListFiltered.Count,
            _ => 0
        };

        if (activeCount == 0 && !string.IsNullOrWhiteSpace(searchClassCode) && GlobalAppStateViewModel.lfc != null)
        {
            IsSearchingOnServer = true;
            var tabSnapshot = SelectedCollectionsTab;
            _ = SearchCollectionOnServerAsync(searchClassCode, searchProfessional, tabSnapshot, token)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted && task.Exception != null)
                        System.Diagnostics.Debug.WriteLine($"Erro nÃ£o tratado na busca de coleÃ§Ã£o: {task.Exception.GetBaseException().Message}");
                }, TaskContinuationOptions.OnlyOnFaulted);
        }
        else
            IsSearchingOnServer = false;

        ClearSelectedIfNotInCurrentFilteredVisible();
    }

    /// <summary>
    /// Busca uma coleÃ§Ã£o no servidor quando nÃ£o encontrada na lista local da sub-aba ativa.
    /// SÃ³ exibe o resultado se a categoria (normal / vencida / exclusÃ£o pendente) corresponder Ã  sub-aba.
    /// </summary>
    private async Task SearchCollectionOnServerAsync(string classCode, string professional, CollectionsTabKind tabWhenRequested, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { IsSearchingOnServer = false; });
                return;
            }

            var pt = await GlobalAppStateViewModel.lfc.GetProfessionalTask(classCode);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSearchingOnServer = false;

                if (cancellationToken.IsCancellationRequested)
                    return;

                if (SelectedCollectionsTab != tabWhenRequested)
                    return;

                if (pt == null)
                {
                    ShowNoResultsMessage = true;
                    return;
                }

                var category = GetCollectionSearchCategory(pt);
                var expectedTab = TabForSearchCategory(category);
                if (tabWhenRequested != expectedTab)
                {
                    MergeServerFetchedTaskForCategory(pt, category);
                    ApplyLocalFilterAllSources(classCode, professional);
                    ShowNoResultsMessage = true;
                    ClearSelectedIfNotInCurrentFilteredVisible();
                    return;
                }

                MergeServerFetchedTaskForCategory(pt, category);
                ApplyLocalFilterAllSources(classCode, professional);
                ShowNoResultsMessage = false;
                ClearSelectedIfNotInCurrentFilteredVisible();
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { IsSearchingOnServer = false; });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao buscar coleÃ§Ã£o no servidor: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSearchingOnServer = false;
                ShowNoResultsMessage = true;
            });
        }
    }

    public async Task UpdateProgressBars()
    {
            try
            {
                //var stopwatch = Stopwatch.StartNew();

                if (SelectedCollection == null || SelectedCollection.companyUsername == null || SelectedCollection.classCode == null || SelectedCollection.UploadComplete == null)
                    return;

                var selected = SelectedCollection;
                ServerProgressValues = await GlobalAppStateViewModel.lfc.UpdateAndGetServerProgress(selected.companyUsername, selected.classCode);


                if (ServerProgressValues == null)
                        return;
                TotalPhotosForRecognition = ServerProgressValues.total;
                TotalPhotosForRecognitionDone = ServerProgressValues.done;
                TotalPhotosForAutoTreatment = ServerProgressValues.total;
                TotalPhotosForAutoTreatmentDone = ServerProgressValues.autoTreated;
                TotalPhotosForOCR = ServerProgressValues.total;
                TotalPhotosForOCRDone = ServerProgressValues.ocr;

                //if (ServerProgressValues.uploaded < ServerProgressValues.total || ServerProgressValues.total == null)
                //{
                //    //UploadComplete = false;
                //}
                //else
                //{
                //    //UploadComplete = true;
                //}
                selected.UploadComplete = (ServerProgressValues.uploaded < ServerProgressValues.total || ServerProgressValues.total == null) ? false : true;
                //stopwatch.Stop();
                //Console.WriteLine($"UpdateProgressBarAndButtons levou {stopwatch.ElapsedMilliseconds} ms");
            }
            catch
            {

            }
            finally
            {

            }
    }
    private async void LoadGraduatesData(ProfessionalTask professionalTask)
    {
        GraduatesData.Clear();
        var result = await GlobalAppStateViewModel.lfc.GetGraduatesByCPFByClassCode(professionalTask.classCode);
        var graduates = result.Content;
        foreach (var g in graduates)
        {
            GraduatesData.Add(g);
        }
        SortGraduatesDataAlphabetically();
        UpdateHdBackupStateFromGraduatesData();
    }

    public async Task UpdateClassSeparationFile(string classCode)
    {
        List<ClassSeparationFile> sepFiles = new List<ClassSeparationFile>();

        var saveFolderPath = SharedClientSide.Helpers.Constants.SeparationFolder + "/" + classCode + "/Save";
        var redirectFilePath = SharedClientSide.Helpers.Constants.SeparationFolder + "/" + classCode + "/redirect.txt";
        if (File.Exists(redirectFilePath))
            saveFolderPath = File.ReadAllText(redirectFilePath) + "/Save";

        var localFilePath = SharedClientSide.Helpers.Constants.GetSaveFolder(classCode) + "/separacao.hermes";
        if (File.Exists(localFilePath))
            sepFiles.Add(new ClassSeparationFile()
            {
                Creator = "Este computador.",
                FileLocationType = ClassSeparationFile.FileLocationTypes.LOCAL,
                ModificationDate = File.GetLastWriteTime(localFilePath),
                FilePathInLocalDisk = localFilePath,
                SeparationProgressFilePath = localFilePath.Replace("separacao.hermes", "separationProgress.txt")
            });

        var localFilePathInLegacySaveFolder = SharedClientSide.Helpers.Constants.GetLegacySaveFolder(classCode) + "/separacao.hermes";
        if (File.Exists(localFilePathInLegacySaveFolder))
            sepFiles.Add(new ClassSeparationFile()
            {
                Creator = "Este computador",
                FileLocationType = ClassSeparationFile.FileLocationTypes.LOCAL,
                ModificationDate = File.GetLastWriteTime(localFilePath),
                FilePathInLocalDisk = localFilePathInLegacySaveFolder,
                SeparationProgressFilePath = localFilePathInLegacySaveFolder.Replace("separacao.hermes", "separationProgress.txt")
            });

        ClassSeparationFiles.Clear();
        foreach (var f in sepFiles) { ClassSeparationFiles.Add(f); }

        var userFiles = await LesserFunctionClient.DefaultClient.GetUserFilesInClass(classCode);
        if (userFiles != null && userFiles.success)
        {
            foreach (var f in userFiles.Content ?? new List<UserFileRecord>())
            {
                if (f.blobName == "")
                    continue;
                if (f.blobName.EndsWith("separacao.hermes") == false)
                    continue;

                var pathParts = f.blobName.Split('/');
                if (pathParts.Length < 2)
                    continue;
                    
                var user = pathParts[pathParts.Length - 2];
                var company = pathParts[0];
                var cloudFilePathInCompanyFolder = f.blobName.Substring(company.Length + 1);
                ClassSeparationFiles.Add(
                    new ClassSeparationFile()
                    {
                        SeparationProgressPathCloudCompanyFolder = cloudFilePathInCompanyFolder.Replace("separacao.hermes", "separationProgress.txt"),
                        FilePathInCloudCompanyFolder = cloudFilePathInCompanyFolder,
                        Creator = user,
                        ModificationDate = f.modificationDate.Value.Date,
                        FileLocationType = ClassSeparationFile.FileLocationTypes.CLOUD,
                        StorageLocation = f.storageLocation
                    }
                );
            }
        }
        if (ClassSeparationFiles.Count == 0)
        {
            ClassSeparationFilesIsVisible = false;
            SelectedSeparationFile = null;
        }
        else
        {
            ClassSeparationFilesIsVisible = true;
            SelectedSeparationFile = ClassSeparationFiles[0];
        }
    }

    /// <summary>Atualiza a view da coleÃ§Ã£o selecionada (servidor + tagadas). Chamado ao clicar na coleÃ§Ã£o e pelos timers quando nÃ£o estÃ£o em 100%.</summary>
    public async void UpdateCollectionViewSelected()
    {
        if (SelectedCollection == null) return;
        try
        {
            ActiveComponent = ActiveViews.CollectionView;
            ExpanderAdvancedOptions = false;

            await RefreshServerProgressAndRelatedUI();
            await UpdateSeparationProgress();

            OnPropertyChanged(nameof(IsDeletionDateNear));
            OnPropertyChanged(nameof(DeletionDateForeground));
            OnPropertyChanged(nameof(ShowDeletionDateAlertIcon));
            OnPropertyChanged(nameof(DeletionDateFontWeight));

            RefreshServerProgressPollingState();
            RefreshSeparationProgressPollingState();
        }
        catch (Exception ex)
        {
            Console.WriteLine("UpdateCollectionViewSelected: " + ex.Message);
        }
    }

    public double ConvertCentsToDecimal(int? cents)
    {
        if (cents is null)
            return 0;
        // DivisÃ£o em ponto flutuante: int/int truncaria (ex.: 5/100 == 0).
        return cents.Value / 100.0;
    }
    public int ConvertDecimalToCents(double? decimalValue)
    {
        if (decimalValue is null)
            return 0;
        return (int)(decimalValue.Value * 100);
    }
    /// <summary>ID da coleÃ§Ã£o: mesmo conjunto que <see cref="RegexHelper.RegexToClassCode"/> (letras, nÃºmeros, _ / ' . -).</summary>
    public bool IsTextAllowed(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return RegexHelper.RegexToClassCode.IsMatch(text);
    }

    public static bool IsClassCodeCharAllowed(char c) =>
        RegexHelper.RegexToClassCode.IsMatch(c.ToString());

    /// <summary>Remove caracteres invÃ¡lidos (ex.: colar texto) quando o TextInput nÃ£o bloqueia.</summary>
    public static string SanitizeClassCodeInput(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;
        return new string(text.Where(IsClassCodeCharAllowed).ToArray());
    }
    
    private void ValidateCollectionName()
    {
        TbCollectionNameIsEmpty = string.IsNullOrWhiteSpace(TbCollectionName);
        TbCollectionNameIsTooShort = !TbCollectionNameIsEmpty
            && (TbCollectionName?.Trim().Length ?? 0) < MinCollectionIdLength;
        TbCollectionNameHasInvalidChars = !TbCollectionNameIsEmpty
            && !TbCollectionNameIsTooShort
            && !IsTextAllowed(TbCollectionName);
        TbCollectionNameHasError = TbCollectionNameIsEmpty || TbCollectionNameIsTooShort || TbCollectionNameHasInvalidChars;
    }
    
    private bool CheckIfClassAlreadyExists(string classCode)
    {
        foreach (ProfessionalTask pt in CollectionsList)
        {
            if (pt.classCode == classCode)
                return true;
        }
        return false;
    }
    public (bool response, string message) CheckIfClassCanBeCreated(ProfessionalTask pt, List<FileInfo> EventFiles, List<FileInfo> RecFiles)
    {
        DirectoryInfo di = new DirectoryInfo(pt.originalEventsFolder);

        List<FileInfo> files = EventFiles.ToArray().Concat(RecFiles).ToList();

        (bool isThereFilepathTooLong, string firstFilepathTooLong) = FileHelper.FileListHasFilepathsLargerThan(files, 200);

        if (isThereFilepathTooLong)
            return (false, "O caminho do arquivo " + firstFilepathTooLong + " ï¿½ muito longo, por isso o Windows nï¿½o permite ao programa acessï¿½-lo. O mï¿½ximo permitido sï¿½o 200 caracteres.");

        (bool isThereFilepathWithProhibitedCharacter, string firstFilepathWithProhibitedCharacter, string prohibitedCharacterFound)
            = FileHelper.FileListHasFilepathWithProhibitedCharacter(files, new string[] { "&", ";" });

        if (isThereFilepathWithProhibitedCharacter)
            return (false, "O caminho " + firstFilepathWithProhibitedCharacter + " possui o caracter " + prohibitedCharacterFound + ", que nï¿½o ï¿½ permitido.");

        foreach (FileInfo c in files)
        {
            if (!SharedClientSide.Helpers.RegexHelper.RegexToFileName.IsMatch(c.Name))
            {
                string chBlocked = "";
                foreach (char ch in c.Name)
                {
                    if (!SharedClientSide.Helpers.RegexHelper.RegexToFileName.IsMatch(ch.ToString()))
                    {
                        chBlocked = ch.ToString();
                        break;
                    }
                }
                return (false, $"Nï¿½o ï¿½ permitido utilizar o caractere '{chBlocked}' no nome do arquivo \"{c.FullName}\".\nRenomeie o arquivo e tente novamente.");
            }
        }

        // ValidaÃ§Ã£o: Verificar se hÃ¡ pelo menos 1 foto na pasta de reconhecimento
        // Exceto quando as fotos jÃ¡ foram separadas (UploadPhotosAreAlreadySorted = true) ou quando for apenas tratamento
        if (!(pt.IsTreatmentOnly ?? false) && !(pt.UploadPhotosAreAlreadySorted ?? false) && RecFiles.Count == 0)
        {
            return (false, "Ã‰ necessÃ¡rio ter pelo menos 1 foto na pasta de reconhecimento para criar a coleÃ§Ã£o. Se as fotos jÃ¡ foram separadas, marque a opÃ§Ã£o 'Fotos jÃ¡ foram separadas'.");
        }

        return (true, "");
    }
    private string GenerateDynamicClassCode()
    {
        return DateTime.Now.ToString("dd_MM_yyyy__HH_mm_");
    }
    private void WaitForProcess(System.Diagnostics.Process p)
    {
        if (p == null)
        {
            Console.WriteLine("O processo ï¿½ nulo");
            return;
        }
        try
        {
            p.WaitForInputIdle();
            p.WaitForExit();
        }
        catch (InvalidOperationException ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk($"Erro ao aguardar o processo: {ex.Message}");
        }
    }
    private async Task LoadProfessionals()
    {
        try
        {
            LoadProfessionalsIsRunning = true;
            if (GlobalAppStateViewModel.lfc != null)
            {
                Professionals = await GlobalAppStateViewModel.lfc.getCompanyProfessionals();

                if(Professionals != null && Professionals.Count > 0)
                    SelectedProfessional = Professionals[0];
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            LoadProfessionalsIsRunning = false;
        }
    }
    private void CheckPathEventFolder()
    {
        try
        {
            if (!Directory.Exists(TbEventFolder))
            {
                TbEventFolderError = true;
                return;
            }
            else
            {
                TbEventFolderError = false;
            }

            // Combo apenas tratamento: exceÃ§Ã£o robusta â€” aceitar qualquer pasta; nÃ£o sobrescrever TbEventFolder nem buscar reconhecimentos
            if (IsTreatmentOnlyCombo)
            {
                TbRecFolder = "";
                return;
            }

            DirectoryInfo inputDir = new DirectoryInfo(TbEventFolder);
            DirectoryInfo eventFolder;

            string dirName = inputDir.Name.ToLower();
            var subDirectories = inputDir.GetDirectories().ToList();

            if (dirName == "1.eventos" || dirName.Contains("1.eventos_grande") || dirName.Contains("event"))
            {
                eventFolder = inputDir;
            }
            else
            {
                eventFolder = subDirectories.Find(x => x.Name.ToLower() == "1.eventos");
                    if (eventFolder == null)
                        eventFolder = subDirectories.Find(x => x.Name.ToLower().Contains("1.eventos_grande"));
                    if (eventFolder == null)
                        eventFolder = subDirectories.Find(x => x.Name.ToLower().Contains("event"));
            }

            DirectoryInfo classFolder;
            if (eventFolder != null)
            {
                classFolder = eventFolder;
                TbEventFolder = eventFolder.FullName;
            }
            else
            {
                classFolder = inputDir;
            }

            DirectoryInfo parentFolder = classFolder.Parent;

            if (parentFolder == null)
                return;
            string parentPath = parentFolder.FullName;
            if (!Directory.Exists(parentPath))
                return;
            classFolder = new DirectoryInfo(parentPath);

            subDirectories = classFolder.GetDirectories().ToList();

            DirectoryInfo recFolder;
            recFolder = subDirectories.Find(x => x.Name.ToLower() == "2.reconhecimentos");
            if (recFolder == null)
                recFolder = subDirectories.Find(x => x.Name.ToLower() == "2.reconhecimentos_grande");
            if (recFolder == null)
                recFolder = subDirectories.Find(x => x.Name.ToLower().Contains("reco"));
            if (recFolder == null)
                recFolder = subDirectories.Find(x => x.Name.ToLower().Contains("id"));
            if (recFolder == null)
                recFolder = subDirectories.Find(x => x.Name.ToLower().Contains("2.id"));
            if (recFolder != null)
            {
                TbRecFolder = recFolder.FullName;
            }
            else
            {
                TbRecFolder = "";
            }
        }
        catch (UnauthorizedAccessException uex)
        {
            TbEventFolderError = true;
            var denied = FileHelper.GetDeniedPathFromUnauthorizedAccess(uex) ?? TbEventFolder ?? "";
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await GlobalAppStateViewModel.Instance.ShowFolderAccessDeniedDialogAsync(
                    TbEventFolder ?? "",
                    denied,
                    uex,
                    "NÃ£o foi possÃ­vel ler o conteÃºdo da pasta de eventos. O Windows negou permissÃ£o ao listar as subpastas â€” a pasta pode estar bloqueada, ser de outro usuÃ¡rio ou exigir permissÃµes de administrador.");
            });
        }
        catch (IOException ioex)
        {
            TbEventFolderError = true;
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(
                    "NÃ£o foi possÃ­vel acessar a pasta de eventos ao listar subpastas.\n\n" + ioex.Message,
                    "Acesso Ã  pasta");
            });
        }
        finally
        {
        }
    }
    /// <summary>Atualiza dados de tagadas/nÃ£o tagadas do arquivo de separaÃ§Ã£o. Ao final atualiza o estado do timer de polling.</summary>
    private async Task UpdateSeparationProgress()
    {
        if (SelectedSeparationFile == null)
        {
            ResumeSeparationFileComponentIsVisible = false;
            return;
        }


        //BtExportIsRunning = false;

        LabelPercentNotSeparated = "loading...";
        LabelPercentSeparated = "loading...";
        NotSeparatedRectHeight = 0;
        SeparatedRectHeight = 0;

        //if (CompanyWindow == null) return;
        //if (CompanyWindow.LesserFunctionClient == null) return;
        //if(ProfessionalTask == null) return;


        //gridHours.Visibility = Visibility.Collapsed;
        //SeparationProgressGrid.Visibility = Visibility.Collapsed;
        //BtReport.IsEnabled = false;
        //BtExport.IsEnabled = false;

        if (SelectedSeparationFile != null && SelectedSeparationFile.Creator != "")
        {
            if (SelectedSeparationFile.FileLocationType == ClassSeparationFile.FileLocationTypes.LOCAL)
            {
                if (File.Exists(SelectedSeparationFile.SeparationProgressFilePath))
                {
                    // checking if the file starts with '<' can be waived in future versions. This check was introduced because a version
                    // release in July 2022 caused the app to download metadata inside the SeparationProgressFilePath instead of the Json.
                    var str = File.ReadAllText(SelectedSeparationFile.SeparationProgressFilePath);
                    if (str.StartsWith("<"))
                        File.Delete(SelectedSeparationFile.SeparationProgressFilePath);
                    else
                        SeparationProgressValue = JsonConvert.DeserializeObject<SeparationProgress>(str);
                }
            }
            else
            {
                //Redundancia  necessï¿½ria pois ha casos em que alternar rapidamente entre turmas faz com que haja um erro dentro do LesserFunctionClient.General.cs, arquivo que nï¿½o ï¿½ recomendï¿½vel alterar por enquanto.
                //Acredito que o erro seja ocasionado porque a response chega depois que o objeto jï¿½ foi destruï¿½do, ou seja, o objeto que chama o mï¿½todo jï¿½ nï¿½o existe mais.
                //Colocar qualquer tipo de trava para aguardar uma chamada acontecer prejudica a experiï¿½ncia do usuï¿½rio para esse caso.
                //Para conservar a boa experiï¿½ncia do usuï¿½rio, o melhor ï¿½ tratar o erro internamente e deixar o programa seguir sem problemas ou avisos.
                try
                {
                    if (GlobalAppStateViewModel.lfc == null || SelectedCollection == null || SelectedSeparationFile == null)
                        return;

                    if (string.IsNullOrWhiteSpace(SelectedCollection.classCode) ||
                        string.IsNullOrWhiteSpace(SelectedCollection.companyUsername) ||
                        string.IsNullOrWhiteSpace(SelectedSeparationFile.Creator))
                        return;
                    SeparationProgressValue = await GlobalAppStateViewModel.lfc.getSeparationProgress(SelectedCollection.classCode, SelectedSeparationFile.Creator, SelectedCollection.companyUsername);
                }
                catch (Exception ex)
                {
                    SeparationProgressValue = null;
                    Console.WriteLine("Erro ao obter progresso de separaï¿½ï¿½o: " + ex.Message);
                }
            }
        }
        if (SeparationProgressValue == null)
            return;
        if (SelectedCollection == null || SelectedCollection.classCode != SeparationProgressValue.code)
            return;

        ResumeSeparationFileComponentIsVisible = SeparationProgressValue.totalPhotos == 0 ? false : true;


        //labelHours.Content = TranslationHelper.Default.TOTAL_HOURS_WORKING + SeparationProgress.totalHours.ToString("F1");
        //gridHours.Visibility = Visibility.Visible;

        var percentNotSeparated = (double)SeparationProgressValue.emptyPhotos / (double)SeparationProgressValue.totalPhotos;

        LabelPercentNotSeparated = SeparationProgressValue.emptyPhotos + " fotos (" + percentNotSeparated.ToString("P1") + ")";
        LabelPercentSeparated = (SeparationProgressValue.totalPhotos - SeparationProgressValue.emptyPhotos) + " fotos (" + (1 - percentNotSeparated).ToString("P1") + ")";


        const double totalGraphRectHeight = 100;
        NotSeparatedRectHeight = totalGraphRectHeight * percentNotSeparated;
        SeparatedRectHeight = totalGraphRectHeight * (1 - percentNotSeparated);

        RefreshSeparationProgressPollingState();
    }

    /// <summary>
    /// Retorna null em caso de sucesso, ou a mensagem de erro para o caller exibir com await.
    /// </summary>
    public string? UpdateGraduateDataFromFile(FileInfo f)
    {
        if (IsTreatmentOnlyCombo)
            return null;

        var err = ImportGraduatesFromExcel(f, GraduatesData, TbRecFolder, CanAddCPFs, CPFsErrorMessage);
        if (err == null)
            SortGraduatesDataAlphabetically();
        return err;
    }

    public string? ImportGraduatesFromExcel(
        FileInfo f,
        ObservableCollection<GraduateByCPF> graduates,
        string recFolder,
        bool canEditCpfs,
        string cpfsErrorMessage)
    {
        if (!canEditCpfs)
            return cpfsErrorMessage;

        graduates.Clear();
        ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        using var package = new ExcelPackage(f);

        ExcelWorksheet workSheet = package.Workbook.Worksheets[0];
        if (workSheet?.Dimension == null)
            return null;

        // Mapear colunas por cabeÃ§alho (inclui Nome / Name)
        static string NormalizeHeader(string? h)
        {
            return (h ?? "")
                .Trim()
                .ToLowerInvariant()
                .Replace("Ã¡", "a").Replace("Ã ", "a").Replace("Ã£", "a").Replace("Ã¢", "a")
                .Replace("Ã©", "e").Replace("Ãª", "e")
                .Replace("Ã­", "i")
                .Replace("Ã³", "o").Replace("Ã´", "o").Replace("Ãµ", "o")
                .Replace("Ãº", "u")
                .Replace("Ã§", "c");
        }

        var colCount = workSheet.Dimension.End.Column;
        var rowCount = workSheet.Dimension.End.Row;
        var headerToCol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 1; c <= colCount; c++)
        {
            var headerText = workSheet.Cells[1, c].Text;
            var norm = NormalizeHeader(headerText);
            if (!string.IsNullOrWhiteSpace(norm) && !headerToCol.ContainsKey(norm))
                headerToCol[norm] = c;
        }

        bool LooksLikeHeaderRow()
        {
            // Se a primeira cÃ©lula parece um nome de arquivo de imagem, Ã© dado (nÃ£o cabeÃ§alho).
            var firstCell = (workSheet.Cells[1, 1].Text ?? "").Trim().ToLowerInvariant();
            if (firstCell.EndsWith(".jpg") || firstCell.EndsWith(".jpeg") || firstCell.EndsWith(".png")
                || firstCell.EndsWith(".nef") || firstCell.EndsWith(".cr2") || firstCell.EndsWith(".cr3")
                || firstCell.EndsWith(".raw") || firstCell.EndsWith(".crw") || firstCell.EndsWith(".arw")
                || firstCell.EndsWith(".dng") || firstCell.EndsWith(".heic") || firstCell.EndsWith(".raf"))
                return false;

            // Evitar falsos positivos quando uma traduÃ§Ã£o vier vazia (Contains("") seria sempre true).
            var photoKey = NormalizeHeader(TranslationHelper.Default.PHOTO_NAME);
            var idKey = NormalizeHeader(TranslationHelper.Default.ID);
            var maxKey = NormalizeHeader(TranslationHelper.Default.MAX_PHOTOS);
            var maxTreatKey = NormalizeHeader(TranslationHelper.Default.MAX_PHOTOS_FOR_TREATMENT);

            int hits = 0;
            for (int c = 1; c <= colCount; c++)
            {
                var norm = NormalizeHeader(workSheet.Cells[1, c].Text);
                if (string.IsNullOrWhiteSpace(norm))
                    continue;

                // SÃ³ contar "contains" quando o token tem conteÃºdo suficiente.
                if ((!string.IsNullOrWhiteSpace(photoKey) && photoKey.Length >= 3 && norm.Contains(photoKey))
                    || norm == "photo" || norm == "foto" || norm.Contains("shortpath") || norm.Contains("short path"))
                    hits++;
                else if ((!string.IsNullOrWhiteSpace(idKey) && idKey.Length >= 2 && norm == idKey) || norm == "cpf" || norm == "id")
                    hits++;
                else if (norm == "email" || norm == "e-mail" || norm.Replace(" ", "") == "email")
                    hits++;
                else if ((!string.IsNullOrWhiteSpace(maxKey) && maxKey.Length >= 3 && norm.Contains(maxKey)) || norm.Contains("max fotos") || norm.Contains("max photos"))
                    hits++;
                else if ((!string.IsNullOrWhiteSpace(maxTreatKey) && maxTreatKey.Length >= 3 && norm.Contains(maxTreatKey)) || norm.Contains("trat") || norm.Contains("treatment"))
                    hits++;

                if (hits >= 2)
                    return true;
            }
            return false;
        }

        int getCol(params string[] keys)
        {
            foreach (var k in keys)
            {
                var nk = NormalizeHeader(k);
                if (headerToCol.TryGetValue(nk, out var col))
                    return col;
            }
            return -1;
        }

        var colPhoto = getCol(TranslationHelper.Default.PHOTO_NAME, "photo", "foto", "nome da foto", "shortpath", "short path");
        var colCpf = getCol(TranslationHelper.Default.ID, "cpf", "id");
        var colName = getCol("nome", "name");
        var colEmail = getCol("email", "e-mail", "e mail");
        var colMaxPhotos = getCol(TranslationHelper.Default.MAX_PHOTOS, "max photos", "max fotos");
        var colMaxTreatment = getCol(TranslationHelper.Default.MAX_PHOTOS_FOR_TREATMENT, "max photos for treatment", "max fotos p/ tratar", "max fotos para tratamento");
        var colBlocked = getCol(TranslationHelper.Default.BLOCKED, "blocked", "bloqueado");
        var colBlockMode = getCol(TranslationHelper.Default.BLOCK_MODE, "block mode", "block type", "tipo de bloqueio");

        static string ExcelColumnName(int colNumber)
        {
            if (colNumber <= 0) return "?";
            var name = "";
            while (colNumber > 0)
            {
                colNumber--;
                name = (char)('A' + (colNumber % 26)) + name;
                colNumber /= 26;
            }
            return name;
        }

        // Fallback compatÃ­vel com layout antigo (sem a coluna Nome)
        if (colPhoto == -1) colPhoto = 1;
        if (colCpf == -1) colCpf = 2;
        if (colName == -1) colName = 3;
        if (colEmail == -1) colEmail = headerToCol.ContainsKey(NormalizeHeader("nome")) || headerToCol.ContainsKey(NormalizeHeader("name")) ? 4 : 3;
        if (colMaxPhotos == -1) colMaxPhotos = colEmail + 1;
        if (colMaxTreatment == -1) colMaxTreatment = colMaxPhotos + 1;
        if (colBlocked == -1) colBlocked = colMaxTreatment + 1;
        if (colBlockMode == -1) colBlockMode = colBlocked + 1;

        var hasHeader = LooksLikeHeaderRow();
        var startRow = hasHeader ? 2 : 1;

        for (int i = startRow; i <= rowCount && i <= 1_000_000; i++)
        {
            GraduateByCPF gradByCPF = new GraduateByCPF { Name = "" };

            var photoPathCell = workSheet.Cells[i, colPhoto].Value;
            if (photoPathCell == null)
                break;

            var recFolderNorm = (recFolder ?? "").Replace("\\", "/").TrimEnd('/');
            var photoPath = photoPathCell.ToString().Replace("\\", "/");
            if (!string.IsNullOrEmpty(recFolderNorm) && photoPath.StartsWith(recFolderNorm, StringComparison.OrdinalIgnoreCase))
                photoPath = photoPath.Replace(recFolderNorm, "", StringComparison.OrdinalIgnoreCase).TrimStart('/');

            if (string.IsNullOrWhiteSpace(recFolderNorm) || !Directory.Exists(recFolderNorm))
                return "A pasta de reconhecimentos especificada nÃ£o existe.";

            if (!File.Exists(recFolderNorm + "/" + photoPath))
            {
                return "O arquivo " + recFolderNorm + "/" + photoPath + " nÃ£o existe.";
            }
            gradByCPF.ShortPath = photoPath;

            var CPFCell = workSheet.Cells[i, colCpf].Value;
            if (CPFCell != null)
                gradByCPF.CPF = CPFCell.ToString();

            var nameCell = workSheet.Cells[i, colName].Value;
            if (nameCell != null)
                gradByCPF.Name = nameCell.ToString();

            var emailCell = workSheet.Cells[i, colEmail].Value;
            if (emailCell != null)
                gradByCPF.Email = emailCell.ToString();

            var maxPhotosCell = workSheet.Cells[i, colMaxPhotos].Value;
            if (maxPhotosCell == null || string.IsNullOrWhiteSpace(maxPhotosCell.ToString()))
            {
                gradByCPF.MaxPhotos = null;
            }
            else
            {
                var raw = maxPhotosCell.ToString();
                var onlyDigits = StringHelper.RemoveAllCharactersButNumbers(raw);
                if (string.IsNullOrWhiteSpace(onlyDigits) || !int.TryParse(onlyDigits, out var parsed))
                {
                    var header = hasHeader ? workSheet.Cells[1, colMaxPhotos].Text : "(sem cabeçalho)";
                    var colExcel = ExcelColumnName(colMaxPhotos);
                    return $"Excel formatado de forma errada — importação interrompida.\n\n" +
                           $"Linha: {i}\n" +
                           $"Campo: {TranslationHelper.Default.MAX_PHOTOS}\n" +
                           $"Coluna (Excel): {colExcel} (#{colMaxPhotos})\n" +
                           $"Cabeçalho: \"{header}\"\n" +
                           $"Valor encontrado: \"{raw}\"\n\n" +
                           "Esperado: um número inteiro (ex.: 10) ou célula vazia.\n" +
                           "Corrija o Excel e tente novamente.";
                }
                gradByCPF.MaxPhotos = parsed;
            }

            var maxPhotosForTreatmentCell = workSheet.Cells[i, colMaxTreatment].Value;
            if (maxPhotosForTreatmentCell == null || string.IsNullOrWhiteSpace(maxPhotosForTreatmentCell.ToString()))
            {
                gradByCPF.MaxPhotosForTreatmentRequest = null;
            }
            else
            {
                var raw = maxPhotosForTreatmentCell.ToString();
                var onlyDigits = StringHelper.RemoveAllCharactersButNumbers(raw);
                if (string.IsNullOrWhiteSpace(onlyDigits) || !int.TryParse(onlyDigits, out var parsed))
                {
                    var header = hasHeader ? workSheet.Cells[1, colMaxTreatment].Text : "(sem cabeçalho)";
                    var colExcel = ExcelColumnName(colMaxTreatment);
                    return $"Excel formatado de forma errada — importação interrompida.\n\n" +
                           $"Linha: {i}\n" +
                           $"Campo: {TranslationHelper.Default.MAX_PHOTOS_FOR_TREATMENT}\n" +
                           $"Coluna (Excel): {colExcel} (#{colMaxTreatment})\n" +
                           $"Cabeçalho: \"{header}\"\n" +
                           $"Valor encontrado: \"{raw}\"\n\n" +
                           "Esperado: um número inteiro (ex.: 10) ou célula vazia.\n" +
                           "Corrija o Excel e tente novamente.";
                }
                gradByCPF.MaxPhotosForTreatmentRequest = parsed;
            }

            var blockedCellValue = workSheet.Cells[i, colBlocked].Value ?? false;
            if (blockedCellValue == null || string.IsNullOrWhiteSpace(blockedCellValue.ToString()))
            {
                gradByCPF.Blocked = false;
            }
            else
            {
                var blockedCell = blockedCellValue.ToString().ToLower();
                if (blockedCell != null && (blockedCell as string) != "")
                {
                    var trueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "sim", "s", "v", "true", "t","verdadeiro","yes","y", "si","1"
                        };
                    var falseValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "nï¿½o","nao", "no", "not", "n", "false","f","ï¿½","falso","0"
                        };
                    if (trueValues.Contains(blockedCell))
                    {
                        gradByCPF.Blocked = true;
                    }
                    else if (falseValues.Contains(blockedCell))
                    {
                        gradByCPF.Blocked = false;
                    }
                }
            }
            var blockTypeCellValue = workSheet.Cells[i, colBlockMode].Value;
            if (blockTypeCellValue == null || string.IsNullOrWhiteSpace(blockTypeCellValue.ToString()))
            {
                gradByCPF.BlockType = "watermark";
            }
            else
            {
                var blockTypeCell = blockTypeCellValue.ToString().ToLower();
                if (blockTypeCell != null && (blockTypeCell as string) != "")
                {
                    var trueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "watermark","marca dagua","marca d'agua","marca d agua","sim", "s", "v", "true", "t","verdadeiro","yes","y", "si","1"
                        };
                    var falseValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "acesso negado","acesso_negado","negado","access denied","access_denied","nï¿½o", "no", "not", "n","nao", "false","f","ï¿½","falso","0"
                        };
                    if (trueValues.Contains(blockTypeCell))
                    {
                        gradByCPF.BlockType = "WATERMARK";
                    }
                    else if (falseValues.Contains(blockTypeCell))
                    {
                        gradByCPF.BlockType = "ACCESS_DENIED";
                    }
                }
            }
            graduates.Add(gradByCPF);
        }
        return null;
    }

    public async Task GenerateAndOpenExcelForGraduatesAsync(
        ObservableCollection<GraduateByCPF> graduates,
        string recFolder,
        bool canEditCpfs,
        string cpfsErrorMessage)
    {
        if (!canEditCpfs)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(cpfsErrorMessage);
            return;
        }

        if (!Directory.Exists(recFolder))
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Recognition folder dosent exist"));
            return;
        }

        ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        using var excel = new ExcelPackage();
        excel.Workbook.Worksheets.Add("Worksheet");
        var excelWorksheet = excel.Workbook.Worksheets["Worksheet"];

        List<string[]> cellsData = new List<string[]>()
        {
            new string[] { TranslationHelper.Default.PHOTO_NAME, TranslationHelper.Default.ID, Loc.Tr("Name"), "Email", TranslationHelper.Default.MAX_PHOTOS, TranslationHelper.Default.MAX_PHOTOS_FOR_TREATMENT, TranslationHelper.Default.BLOCKED, TranslationHelper.Default.BLOCK_MODE }
        };
        string headerRange = "A1:" + Char.ConvertFromUtf32(cellsData[0].Length + 64) + "1";
        excelWorksheet.Cells[headerRange].Style.Font.Bold = true;
        excelWorksheet.Column(1).Width = 100;
        excelWorksheet.Column(2).Width = 30;
        excelWorksheet.Column(3).Width = 30;
        excelWorksheet.Column(4).Width = 30;
        excelWorksheet.Column(5).Width = 30;
        excelWorksheet.Column(6).Width = 30;
        excelWorksheet.Column(7).Width = 18;
        excelWorksheet.Column(8).Width = 18;

        foreach (var g in graduates)
        {
            if (g.BlockType == null)
                g.BlockType = "WATERMARK";
            cellsData.Add(new string[]
            {
                g.ShortPath,
                g.CPF,
                g.Name ?? "",
                g.Email,
                g.MaxPhotos.ToString(),
                g.MaxPhotosForTreatmentRequest.ToString(),
                g.Blocked.ToString(),
                g.BlockType.ToString()
            });
        }
        excelWorksheet.Cells[1, 1].LoadFromArrays(cellsData);

        var excelFile = new FileInfo(recFolder.TrimEnd('\\', '/') + "/Excel/" +
            DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + "-" + DateTime.Now.Hour + "-" + DateTime.Now.Minute + "-" + DateTime.Now.Second + "-" + DateTime.Now.Millisecond + ".xlsx");

        try
        {
            Directory.CreateDirectory(excelFile.Directory!.FullName);
        }
        catch
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("A pasta " + excelFile.Directory!.FullName + " nÃ£o pÃ´de ser criada. Verifique se estÃ¡ acessÃ­vel.");
            return;
        }
        excel.SaveAs(excelFile);

        var importError1 = ImportGraduatesFromExcel(excelFile, graduates, recFolder, canEditCpfs, cpfsErrorMessage);
        if (importError1 != null)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(importError1, "Erro");
            return;
        }

        var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = excelFile.ToString(),
            UseShellExecute = true
        };
        p.Start();

        await Task.Run(() => WaitForProcess(p));

        var importError2 = ImportGraduatesFromExcel(excelFile, graduates, recFolder, canEditCpfs, cpfsErrorMessage);
        if (importError2 != null)
            GlobalAppStateViewModel.Instance.ShowDialogOk(importError2, "Erro");
    }

    /// <summary>
    /// Ordena a lista GraduatesData alfabeticamente crescente pelo nome da foto (ShortPath)
    /// </summary>
    private void SortGraduatesDataAlphabetically()
    {
        if (GraduatesData == null || GraduatesData.Count == 0)
            return;
            
        var sortedList = GraduatesData.OrderBy(g => g.ShortPath).ToList();
        GraduatesData.Clear();
        foreach (var item in sortedList)
        {
            GraduatesData.Add(item);
        }
    }
    
    public async void GetInfosAboutFreeTrialPeriod()
    {
        try
        {
            if (GlobalAppStateViewModel.lfc != null)
            {
                var result = await GlobalAppStateViewModel.lfc.GetRemainingFreeTrialPhotos2();
                if (result.success)
                {
                    RemainingFreeTrialPhotosResult = result.Content;
                }
            }
            return;
        }
        catch (Exception e)
        {
            return;
        }
    }
    #region RELAY COMMANDS
    [RelayCommand]
    public void BackLasViewCommand()
    {
        // Se estÃ¡vamos em MessagesView e tinha uma coleÃ§Ã£o selecionada antes, restaurar
        if (ActiveComponent == ActiveViews.MessagesView && _lastSelectedCollection != null)
        {
            // Restaurar a coleÃ§Ã£o selecionada antes de mudar a view
            // (evita que OnActiveComponentChanged limpe a referÃªncia)
            var collectionToRestore = _lastSelectedCollection;
            _lastSelectedCollection = null;
            
            ActiveComponent = lastActiveComponent;
            SelectedCollection = collectionToRestore;
        }
        else
        {
            ActiveComponent = lastActiveComponent;
        }
    }
    [RelayCommand]
    public void OpenQuickAccessViewCommand()
    {
        // Sempre mostra os combos (QuickAccess) para criar nova coleï¿½ï¿½o
        SelectedCollection = null;
        ActiveComponent = ActiveViews.QuickAccess;
    }
    
    [RelayCommand]
    public void ShowNewsCommand()
    {
        // Sempre mostra as novidades (usado pelo botï¿½o Home)
        SelectedCollection = null;
        ActiveComponent = ActiveViews.NewsView;
    }

    [RelayCommand]
    public void OpenNewCollectionViewCommand()
    {
        SelectedCollection = null;
        IsReupload = false;
        CurrentProfessionalName = SelectedProfessional.username ?? GlobalAppStateViewModel.lfc.loginResult.User.company;
        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.NewCollection; // Abre a tela de nova coleï¿½ï¿½o personalizada
        _preConfiguredComboAuthority = null;

        GraduatesData.Clear();
        TbCollectionName = GenerateDynamicClassCode();
        TbEventFolder = string.Empty;
        TbRecFolder = string.Empty;
        CbUploadedPhotosAreAlreadySorted = false;
        CbAllowCPFsToSeeAllPhotos = false;
        CbHDBackup = false;
        CbEnableAutoExclusion = true;
        CbEnablePhotoSales = false;
        CbPhotosCannotHaveWatermarks = false;
        TbPricePerPhotoForSellingOnline = 0;
        TbTotalPhotosForFreePerGraduate = 0;
        TbProfessionalTaskDescription = string.Empty;
        CbEnableAutoTreatment = false;
        CbOcr = false;
        
        // Resetar propriedade de combo apenas tratamento
        IsTreatmentOnlyCombo = false;
        
        // Resetar propriedades de bloqueio de CPFs, HD e AutoTreatment
        CanAddCPFs = true;
        CPFsErrorMessage = string.Empty;
        CbHDBackupIsDisabled = false;
        CbHDBackupErrorMessage = string.Empty;
        CbEnableAutoTreatmentIsDisabled = false;
        CbEnableAutoTreatmentErrorMessage = string.Empty;
        CbUploadedPhotosAreAlreadySortedIsDisabled = false;
        
        // Resetar propriedades de armazenamento HD
        IsHDStorageOptionsVisible = false;
        CbHDStorageThreeMonths = false;
        CbHDStorageTwoYears = false;
        CbHDStorageFiveYears = false;

        ExpanderAdvancedOptionsIsEnabled = true;
    }
    [RelayCommand]
    public void OpenNewCollectionPreConfiguredOnlySeparationCommand()
    {
        SelectedCollection = null;
        IsReupload = false;
        CurrentProfessionalName = SelectedProfessional.username ?? GlobalAppStateViewModel.lfc.loginResult.User.company;
        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.NewCollectionPreConfigured;
        _preConfiguredComboAuthority = null;

        GraduatesData.Clear();
        TbCollectionName = GenerateDynamicClassCode();
        TbEventFolder = string.Empty;
        TbRecFolder = string.Empty;
        CbUploadedPhotosAreAlreadySorted = false;
        CbAllowCPFsToSeeAllPhotos = false;
        CbHDBackup = false;
        CbEnableAutoExclusion = true;
        CbEnablePhotoSales = false;
        CbPhotosCannotHaveWatermarks = false;
        TbPricePerPhotoForSellingOnline = 0;
        TbTotalPhotosForFreePerGraduate = 0;
        TbProfessionalTaskDescription = string.Empty;
        CbEnableAutoTreatment = false;
        CbOcr = false;
        
        // Resetar propriedade de combo apenas tratamento
        IsTreatmentOnlyCombo = false;
        
        // Resetar propriedades de bloqueio de CPFs, HD e AutoTreatment
        CanAddCPFs = true;
        CPFsErrorMessage = string.Empty;
        CbHDBackupIsDisabled = false;
        CbHDBackupErrorMessage = string.Empty;
        CbEnableAutoTreatmentIsDisabled = false;
        CbEnableAutoTreatmentErrorMessage = string.Empty;
        CbUploadedPhotosAreAlreadySortedIsDisabled = false;
        
        // Resetar propriedades de armazenamento HD
        IsHDStorageOptionsVisible = false;
        CbHDStorageThreeMonths = false;
        CbHDStorageTwoYears = false;
        CbHDStorageFiveYears = false;

        ExpanderAdvancedOptions = false;
        ExpanderAdvancedOptionsIsEnabled = false;
    }

    [RelayCommand]
    public void OpenNewCollectionPreConfigured(CollectionComboOptions options)
    {
        SelectedCollection = null;
        IsReupload = false;
        CurrentProfessionalName = SelectedProfessional.username ?? GlobalAppStateViewModel.lfc.loginResult.User.company;
        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.NewCollectionPreConfigured;
        _preConfiguredComboAuthority = options;
        GraduatesData.Clear();
        TbCollectionName = GenerateDynamicClassCode();
        TbEventFolder = string.Empty;
        TbRecFolder = string.Empty;
        CbUploadedPhotosAreAlreadySorted = options.UploadedPhotosAreAlreadySorted;
        CbAllowCPFsToSeeAllPhotos = options.AllowCPFsToSeeAllPhotos;
        CbHDBackup = options.BackupHd;
        CbEnableAutoExclusion = true;
        CbEnablePhotoSales = options.EnablePhotoSales;
        CbPhotosCannotHaveWatermarks = false;
        TbPricePerPhotoForSellingOnline = 0;
        TbTotalPhotosForFreePerGraduate = 0;
        TbProfessionalTaskDescription = string.Empty;
        CbEnableAutoTreatment = options.AutoTreatment;
        CbOcr = options.Ocr;
        CbAllowDeletedProductionToBeFoundAnyone = options.AllowDeletedProductionToBeFoundAnyone;
        
        // Definir se Ã© um combo apenas tratamento
        IsTreatmentOnlyCombo = options.IsTreatmentOnly;
        
        // Resetar propriedades de bloqueio de CPFs, HD e AutoTreatment
        CanAddCPFs = true;
        CPFsErrorMessage = string.Empty;
        CbHDBackupIsDisabled = false;
        CbHDBackupErrorMessage = string.Empty;
        CbEnableAutoTreatmentIsDisabled = false;
        CbEnableAutoTreatmentErrorMessage = string.Empty;
        
        // Resetar propriedades de armazenamento HD (serÃ¡ atualizado pelo OnCbHDBackupChanged se HD estiver marcado)
        IsHDStorageOptionsVisible = options.BackupHd == true;
        ApplyHdStoragePeriodFromComboOptions(options);

        ExpanderAdvancedOptions = true;
        ExpanderAdvancedOptionsIsEnabled = false;
    }

    /// <summary>
    /// Aplica o prazo de armazenamento definido no combo (meses no backend) aos toggles 3 meses / 2 anos / 5 anos da criaÃ§Ã£o da turma.
    /// </summary>
    private void ApplyHdStoragePeriodFromComboOptions(CollectionComboOptions options)
    {
        CbHDStorageThreeMonths = false;
        CbHDStorageTwoYears = false;
        CbHDStorageFiveYears = false;
        if (options.BackupHd != true)
            return;

        if (options.StorageTimeMonths.HasValue)
        {
            int m = options.StorageTimeMonths.Value;
            int best = new[] { 3, 24, 60 }.OrderBy(b => Math.Abs(b - m)).First();
            if (best == 3)
                CbHDStorageThreeMonths = true;
            else if (best == 24)
                CbHDStorageTwoYears = true;
            else
                CbHDStorageFiveYears = true;
            return;
        }

        // Sem meses explÃ­citos no backend: manter o padrÃ£o jÃ¡ usado no app (5 anos quando HD estÃ¡ no combo)
        CbHDStorageFiveYears = true;
    }

    [RelayCommand]
    public async Task OpenAddIdsViewCommand()
    {
        if (SelectedCollection == null || string.IsNullOrWhiteSpace(SelectedCollection.classCode))
            return;

        IsReupload = false;

        CurrentProfessionalName = SelectedCollection.professionalLogin;
        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.AddIds;
        _preConfiguredComboAuthority = null;

        TbCollectionName = SelectedCollection.classCode;

        // Tentar popular a pasta de reconhecimentos local para o seletor de arquivos.
        TbEventFolder = SelectedCollection.originalEventsFolder;
        TbRecFolder = SelectedCollection.originalRecFolder;
        if (string.IsNullOrWhiteSpace(TbRecFolder) || !Directory.Exists(TbRecFolder))
        {
            // Se vier apenas a pasta de eventos, tenta inferir a pasta de reconhecimentos pelo layout da turma.
            CheckPathEventFolder();
        }

        // Regras de CPF/ID: mantÃ©m o mesmo bloqueio usado no reupload (outro perÃ­odo + nÃ£o-HD).
        var canAddCpfs = true;
        var cpfsErrorMessage = string.Empty;
        if (IsCollectionFromDifferentBillingPeriod(SelectedCollection) && (SelectedCollection.UploadHD != true))
        {
            canAddCpfs = false;
            cpfsErrorMessage = Loc.Tr("This collection is from another billing period. CPFs cannot be added or modified during reupload.");
        }

        AddIdsVm = new AddIdsViewModel(
            GlobalAppStateViewModel.lfc,
            SelectedCollection,
            CurrentProfessionalName,
            TbRecFolder,
            canAddCpfs,
            cpfsErrorMessage);

        // Abrir rÃ¡pido: navega jÃ¡ com tÃ­tulo/ID/separador preenchidos e carrega a lista em background.
        _ = AddIdsVm.InitializeAsync();
    }

    [RelayCommand]
    public async Task OpenEditIdsViewCommand()
    {
        if (SelectedCollection == null || string.IsNullOrWhiteSpace(SelectedCollection.classCode))
            return;

        IsReupload = false;

        CurrentProfessionalName = SelectedCollection.professionalLogin;
        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.EditIds;
        _preConfiguredComboAuthority = null;

        TbCollectionName = SelectedCollection.classCode;

        TbEventFolder = SelectedCollection.originalEventsFolder;
        TbRecFolder = SelectedCollection.originalRecFolder;
        if (string.IsNullOrWhiteSpace(TbRecFolder) || !Directory.Exists(TbRecFolder))
            CheckPathEventFolder();

        var canEditCpfs = true;
        var cpfsErrorMessage = string.Empty;
        if (IsCollectionFromDifferentBillingPeriod(SelectedCollection) && (SelectedCollection.UploadHD != true))
        {
            canEditCpfs = false;
            cpfsErrorMessage = Loc.Tr("This collection is from another billing period. CPFs cannot be added or modified during reupload.");
        }

        EditIdsVm = new EditIdsViewModel(
            GlobalAppStateViewModel.lfc,
            SelectedCollection,
            CurrentProfessionalName,
            TbRecFolder,
            canEditCpfs,
            cpfsErrorMessage);

        _ = EditIdsVm.InitializeAsync();
    }

    public void SortGraduatesDataAlphabeticallyForUi() => SortGraduatesDataAlphabetically();

    [RelayCommand]
    public void OpenReuploadViewCommand()
    {
        if (SelectedCollection == null)
            return;

        // Ativar flag para prevenir que eventos interfiram durante o carregamento
        _isLoadingReuploadData = true;

        try
        {
            CurrentProfessionalName = SelectedCollection.professionalLogin;

            ScrollComponentNewCollection = 0;
            ActiveComponent = ActiveViews.NewCollection;
            _preConfiguredComboAuthority = null;

            ExpanderAdvancedOptions = true;
            ExpanderAdvancedOptionsIsEnabled = true; // Permitir alteraÃ§Ã£o de todas as configuraÃ§Ãµes no reupload

            IsReupload = true;
            TbCollectionName = SelectedCollection.classCode;
            TbEventFolder = SelectedCollection.originalEventsFolder;
            TbRecFolder = SelectedCollection.originalRecFolder;
            CbUploadedPhotosAreAlreadySorted = SelectedCollection.UploadPhotosAreAlreadySorted;
            // Desabilitar a opÃ§Ã£o de "jÃ¡ estÃ£o separados" durante reupload
            CbUploadedPhotosAreAlreadySortedIsDisabled = true;
            CbAllowCPFsToSeeAllPhotos = SelectedCollection.AllowCPFsToSeeAllPhotos;
            CbEnableAutoExclusion = SelectedCollection.EnableFaceRelevanceDetection;
            CbEnablePhotoSales = SelectedCollection.EnablePhotosSales ?? false;
            CbPhotosCannotHaveWatermarks = SelectedCollection.PhotosCannotHaveWatermarks ?? false;
            TbPricePerPhotoForSellingOnline = ConvertCentsToDecimal(SelectedCollection.PricePerPhotoForSellingOnlineInCents);
            TbTotalPhotosForFreePerGraduate = SelectedCollection.TotalPhotosForFreePerGraduate ?? 0;
            TbProfessionalTaskDescription = SelectedCollection.Description ?? string.Empty;
            CbEnableAutoTreatment = SelectedCollection.AutoTreatment ?? false;
            AutoTreatmentVersion = SelectedCollection.AutoTreatmentVersion;
            CbOcr = SelectedCollection.OCR ?? false;
            CbAllowDeletedProductionToBeFoundAnyone = SelectedCollection.AllowDeletedProductionToBeFoundAnyone ?? false;
            
            // IMPORTANTE: Atribuir CbHDBackup ANTES de verificar as regras de CPF
            CbHDBackup = SelectedCollection.UploadHD ?? false;
            
            // Verificar se o HD deve estar desabilitado devido ao perÃ­odo de faturamento
            if (IsCollectionFromDifferentBillingPeriod(SelectedCollection))
            {
                // Em reupload de outro perÃ­odo de faturamento:
                // - NÃ£o permitir transformar turma NÃƒO-HD em HD (desabilita o toggle)
                // - NÃ£o permitir habilitar AutoTreatment (estÃ¡ ligado ao HD)
                // - Se a turma jÃ¡ Ã© HD, permitir adicionar/editar CPFs
                CbHDBackupIsDisabled = true;
                CbHDBackupErrorMessage = Loc.Tr("This collection is from another billing period, please create a new collection to perform an HD backup.");
                
                // Bloquear AutoTreatment tambÃ©m (estÃ¡ ligado ao HD)
                CbEnableAutoTreatmentIsDisabled = true;
                CbEnableAutoTreatmentErrorMessage = Loc.Tr("This collection is from another billing period, please create a new collection to enable automatic enhancement.");

                // NÃ£o force desabilitar se jÃ¡ for HD; apenas impeÃ§a mudar o estado
                // MantÃ©m CbHDBackup como estÃ¡ (true permanece true; false permanece false)

                // Regras de CPF: turmas NÃƒO-HD nÃ£o podem adicionar/editar CPF; turmas HD podem
                if (CbHDBackup == true)
                {
                    CanAddCPFs = true;
                    CPFsErrorMessage = string.Empty;
                }
                else
                {
                    CanAddCPFs = false;
                    CPFsErrorMessage = Loc.Tr("This collection is from another billing period. CPFs cannot be added or modified during reupload.");
                }
            }
            else
            {
                CbHDBackupIsDisabled = false;
                CbHDBackupErrorMessage = string.Empty;
                CbEnableAutoTreatmentIsDisabled = false;
                CbEnableAutoTreatmentErrorMessage = string.Empty;
                CanAddCPFs = true;
                CPFsErrorMessage = string.Empty;
                // Manter desabilitado durante reupload
                CbUploadedPhotosAreAlreadySortedIsDisabled = true;
            }
            
            LoadGraduatesData(SelectedCollection);
        }
        finally
        {
            // Desativar flag apÃ³s carregar todos os dados
            _isLoadingReuploadData = false;
        }
    }
    [RelayCommand]
    public async Task OpenSelectProfessionalViewCommand()
    {
        // NÃ£o limpar SelectedCollection se estiver visualizando uma coleÃ§Ã£o
        // Isso permite trocar o separador de uma coleÃ§Ã£o existente
        var wasViewingCollection = SelectedCollection != null && ActiveComponent == ActiveViews.CollectionView;
        
        if (!wasViewingCollection)
        {
            SelectedCollection = null;
        }
        
        _isOpeningSelectProfessionalView = true;
        try
        {
            ActiveComponent = ActiveViews.SelectProfessional;
            if (Professionals == null || Professionals.Count == 0)
                await LoadProfessionals();
            
            // Se estiver visualizando uma coleÃ§Ã£o, selecionar o profissional atual da coleÃ§Ã£o (sem disparar save)
            if (wasViewingCollection && SelectedCollection != null)
            {
                var currentProfessional = Professionals?.FirstOrDefault(p => p.username == SelectedCollection.professionalLogin);
                if (currentProfessional != null)
                {
                    SelectedProfessional = currentProfessional;
                }
                else if (SelectedProfessional == null && Professionals != null && Professionals.Count > 0)
                {
                    SelectedProfessional = Professionals[0];
                }
            }
            else if (SelectedProfessional == null && Professionals != null && Professionals.Count > 0)
            {
                SelectedProfessional = Professionals[0];
            }
        }
        finally
        {
            _isOpeningSelectProfessionalView = false;
        }
    }
    [RelayCommand]
    public void OpenCancelBillingViewCommand()
    {
        // NÃ£o limpar SelectedCollection para manter a coleÃ§Ã£o selecionada na tela de cancelamento
        ActiveComponent = ActiveViews.CancelBilling;
    }

    /// <summary>
    /// Atualiza um objeto ProfessionalTask na lista com os dados atualizados do servidor
    /// MantÃ©m a referÃªncia do objeto para evitar problemas de seleÃ§Ã£o na UI
    /// </summary>
    private void UpdateCollectionInList(ProfessionalTask updatedCollection, string classCode)
    {
        if (updatedCollection == null || string.IsNullOrEmpty(classCode))
            return;

        // Encontrar o objeto na lista CollectionsList pelo classCode
        var collectionInList = CollectionsList.FirstOrDefault(c => c.classCode == classCode);
        if (collectionInList != null)
        {
            // Atualizar todas as propriedades relevantes do objeto na lista
            // Isso mantÃ©m a referÃªncia do objeto, evitando problemas de seleÃ§Ã£o
            collectionInList.professionalLogin = updatedCollection.professionalLogin;
            collectionInList.ScheduledDeletionDate = updatedCollection.ScheduledDeletionDate;
            collectionInList.BillingCancelled = updatedCollection.BillingCancelled;
            collectionInList.UploadComplete = updatedCollection.UploadComplete;
            collectionInList.UploadHD = updatedCollection.UploadHD;
            collectionInList.AutoTreatment = updatedCollection.AutoTreatment;
            collectionInList.OCR = updatedCollection.OCR;
            collectionInList.EnablePhotosSales = updatedCollection.EnablePhotosSales;
            collectionInList.PhotosCannotHaveWatermarks = updatedCollection.PhotosCannotHaveWatermarks;
            collectionInList.PricePerPhotoForSellingOnlineInCents = updatedCollection.PricePerPhotoForSellingOnlineInCents;
            collectionInList.TotalPhotosForFreePerGraduate = updatedCollection.TotalPhotosForFreePerGraduate;
            collectionInList.Status = updatedCollection.Status;
            collectionInList.StorageLocation = updatedCollection.StorageLocation;
            collectionInList.CreationDate = updatedCollection.CreationDate;
            collectionInList.DeletionDate = updatedCollection.DeletionDate;
            collectionInList.EnqueuedForDeletion = updatedCollection.EnqueuedForDeletion;
            collectionInList.IsDeleted = updatedCollection.IsDeleted;
            collectionInList.recognitionPhotos = updatedCollection.recognitionPhotos;
            collectionInList.eventPhotos = updatedCollection.eventPhotos;
            collectionInList.photosSeparatedByProfessional = updatedCollection.photosSeparatedByProfessional;
            
            // Atualizar tambÃ©m na lista filtrada se existir
            var collectionInFilteredList = CollectionsListFiltered.FirstOrDefault(c => c.classCode == classCode);
            if (collectionInFilteredList != null)
            {
                collectionInFilteredList.professionalLogin = updatedCollection.professionalLogin;
                collectionInFilteredList.ScheduledDeletionDate = updatedCollection.ScheduledDeletionDate;
                collectionInFilteredList.BillingCancelled = updatedCollection.BillingCancelled;
                collectionInFilteredList.UploadComplete = updatedCollection.UploadComplete;
                collectionInFilteredList.UploadHD = updatedCollection.UploadHD;
                collectionInFilteredList.AutoTreatment = updatedCollection.AutoTreatment;
                collectionInFilteredList.OCR = updatedCollection.OCR;
                collectionInFilteredList.EnablePhotosSales = updatedCollection.EnablePhotosSales;
                collectionInFilteredList.PhotosCannotHaveWatermarks = updatedCollection.PhotosCannotHaveWatermarks;
                collectionInFilteredList.PricePerPhotoForSellingOnlineInCents = updatedCollection.PricePerPhotoForSellingOnlineInCents;
                collectionInFilteredList.TotalPhotosForFreePerGraduate = updatedCollection.TotalPhotosForFreePerGraduate;
                collectionInFilteredList.Status = updatedCollection.Status;
                collectionInFilteredList.StorageLocation = updatedCollection.StorageLocation;
                collectionInFilteredList.CreationDate = updatedCollection.CreationDate;
                collectionInFilteredList.DeletionDate = updatedCollection.DeletionDate;
                collectionInFilteredList.EnqueuedForDeletion = updatedCollection.EnqueuedForDeletion;
                collectionInFilteredList.IsDeleted = updatedCollection.IsDeleted;
                collectionInFilteredList.recognitionPhotos = updatedCollection.recognitionPhotos;
                collectionInFilteredList.eventPhotos = updatedCollection.eventPhotos;
                collectionInFilteredList.photosSeparatedByProfessional = updatedCollection.photosSeparatedByProfessional;
            }
        }

        ApplyLocalFilterAllSources(FilterClassCode ?? string.Empty, FilterProfessionalText ?? string.Empty);
    }

    public async Task OpenChangeDeletionDateDialogCommand()
    {
        if (SelectedCollection == null || GlobalAppStateViewModel.lfc == null)
            return;

        // Salvar o classCode antes de abrir o diÃ¡logo
        var currentClassCode = SelectedCollection.classCode;

        try
        {
            var viewModel = new ChangeDeletionDateViewModel(
                GlobalAppStateViewModel.lfc,
                SelectedCollection,
                async () =>
                {
                    try
                    {
                        // Busca a coleÃ§Ã£o atualizada do servidor
                        var updatedCollection = await GlobalAppStateViewModel.lfc.GetProfessionalTask(currentClassCode);
                        if (updatedCollection != null)
                        {
                            // Atualizar o objeto na lista com os dados do servidor
                            // Isso mantÃ©m a referÃªncia do objeto, evitando problemas de seleÃ§Ã£o
                            UpdateCollectionInList(updatedCollection, currentClassCode);
                            
                            // Garantir que SelectedCollection ainda aponta para o objeto na lista
                            // Se o classCode ainda corresponde, manter a referÃªncia atual
                            if (SelectedCollection?.classCode == currentClassCode)
                            {
                                // Encontrar o objeto atualizado na lista
                                var collectionInList = CollectionsList.FirstOrDefault(c => c.classCode == currentClassCode);
                                if (collectionInList != null && SelectedCollection != collectionInList)
                                {
                                    // Atualizar a referÃªncia apenas se for diferente
                                    // Isso notifica a UI sem perder a seleÃ§Ã£o
                                    _isUpdatingSelectedCollection = true;
                                    SelectedCollection = collectionInList;
                                    _isUpdatingSelectedCollection = false;
                                }
                                else if (collectionInList != null)
                                {
                                    // Se jÃ¡ Ã© a mesma referÃªncia, apenas notificar mudanÃ§as nas propriedades
                                    // Isso forÃ§a a UI a atualizar sem perder a seleÃ§Ã£o
                                    OnPropertyChanged(nameof(SelectedCollection));
                                }
                            }
                            
                            // Atualizar a view da coleÃ§Ã£o selecionada de forma sÃ­ncrona
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                UpdateCollectionViewSelected();
                                
                                // Notificar mudanÃ§as nas propriedades relacionadas Ã  data de deleÃ§Ã£o
                                OnPropertyChanged(nameof(IsDeletionDatePassed));
                                OnPropertyChanged(nameof(IsDeletionDateNear));
                                OnPropertyChanged(nameof(DeletionDateForeground));
                                OnPropertyChanged(nameof(ShowDeletionDateAlertIcon));
                                OnPropertyChanged(nameof(DeletionDateFontWeight));
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            GlobalAppStateViewModel.Instance.ShowDialogOk($"Erro ao atualizar a coleÃ§Ã£o: {ex.Message}", Loc.Tr("Error"));
                        });
                    }
                }
            );

            var dialog = new ChangeDeletionDateWindow
            {
                DataContext = viewModel
            };
            
            viewModel.SetWindow(dialog);

            // Carrega o preÃ§o apÃ³s o modal estar carregado
            _ = viewModel.LoadInitialPriceAsync();

            await dialog.ShowDialog(MainWindow.instance);
        }
        catch (Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message, Loc.Tr("Error"));
        }
    }
    
    [RelayCommand]
    public async Task DeleteClassCommand()
    {
        if (SelectedCollection == null || string.IsNullOrEmpty(SelectedCollection.classCode))
            return;
        var confirmed = await GlobalAppStateViewModel.Instance.ShowDialogYesNo(
            Loc.Tr("Do you really want to request the deletion of this collection? This action can be cancelled later from the deleted collections list.", "Deseja realmente solicitar a exclusÃ£o desta coleÃ§Ã£o? Esta aÃ§Ã£o pode ser cancelada depois na lista de coleÃ§Ãµes deletadas."),
            Loc.Tr("Delete collection", "Excluir coleÃ§Ã£o"));
        if (!confirmed)
            return;
        try
        {
            var result = await GlobalAppStateViewModel.lfc.RequestDeleteCollection(SelectedCollection.classCode);
            if (result?.loginFailed == true)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(result.message ?? Loc.Tr("Login failed.", "Falha no login."), Loc.Tr("Error", "Erro"));
                return;
            }
            if (result?.success != true)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(result?.message ?? Loc.Tr("Could not request deletion.", "NÃ£o foi possÃ­vel solicitar a exclusÃ£o."), Loc.Tr("Error", "Erro"));
                return;
            }
            await UpdateProfessionalTasksList(null);
            await LoadDeletedCollectionsAsync();
            // Atualiza a lista de vencidas para remover a coleÃ§Ã£o da UI se o usuÃ¡rio estava nessa aba
            await LoadExpiredCollectionsAsync();
        }
        catch (Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message, Loc.Tr("Error", "Erro"));
        }
    }

    /// <summary>True se a lista de deletadas jÃ¡ foi carregada nesta sessÃ£o (carrega sÃ³ na primeira vez ao abrir a aba).</summary>
    private bool _deletedListLoadedOnce;
    /// <summary>True se a lista de vencidas jÃ¡ foi carregada nesta sessÃ£o.</summary>
    private bool _expiredListLoadedOnce;

    /// <summary>Seleciona a aba da lista de coleÃ§Ãµes (Normal, Expired, Deleted). Comando usado pelos botÃµes de aba com CommandParameter.</summary>
    [RelayCommand]
    public void SelectCollectionsTab(object parameter)
    {
        if (parameter is string s && Enum.TryParse<CollectionsTabKind>(s, true, out var tab))
            SelectedCollectionsTab = tab;
    }

    /// <summary>Garante que os dados da aba ativa foram carregados (na primeira vez que o usuÃ¡rio entra na aba).</summary>
    private async void EnsureTabDataLoadedAsync(CollectionsTabKind tab)
    {
        if (tab == CollectionsTabKind.Expired && !_expiredListLoadedOnce)
        {
            _expiredListLoadedOnce = true;
            await LoadExpiredCollectionsAsync();
        }
        else if (tab == CollectionsTabKind.Deleted && !_deletedListLoadedOnce)
        {
            _deletedListLoadedOnce = true;
            await LoadDeletedCollectionsAsync();
        }
    }

    /// <summary>Carrega a lista de coleÃ§Ãµes vencidas (prazo de armazenamento expirado).</summary>
    public async Task LoadExpiredCollectionsAsync()
    {
        if (GlobalAppStateViewModel.lfc == null) return;
        try
        {
            ExpiredCollectionsListIsLoading = true;
            var list = await GlobalAppStateViewModel.lfc.getExpiredCompanyProfessionalTasks();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ExpiredCollectionsList.Clear();
                if (list != null)
                {
                    foreach (var pt in list)
                    {
                        if (pt != null && !string.IsNullOrEmpty(pt.classCode))
                            ExpiredCollectionsList.Add(pt);
                    }
                }
                ApplyLocalFilterAllSources(FilterClassCode ?? string.Empty, FilterProfessionalText ?? string.Empty);
                OnPropertyChanged(nameof(VisibleCollectionsList));
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message, Loc.Tr("Error", "Erro")));
        }
        finally
        {
            ExpiredCollectionsListIsLoading = false;
        }
    }

    /// <summary>Carrega a lista de coleÃ§Ãµes com solicitaÃ§Ã£o de exclusÃ£o pendente (mesmo formato da lista normal: ProfessionalTask).</summary>
    public async Task LoadDeletedCollectionsAsync()
    {
        if (GlobalAppStateViewModel.lfc == null) return;
        try
        {
            DeletedCollectionsListIsLoading = true;
            var result = await GlobalAppStateViewModel.lfc.GetCollectionsRequestedToDeletion();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DeletedCollectionsList.Clear();
                if (result?.Content != null)
                {
                    foreach (var pt in result.Content)
                    {
                        if (pt != null && !string.IsNullOrEmpty(pt.classCode))
                            DeletedCollectionsList.Add(pt);
                    }
                }
                ApplyLocalFilterAllSources(FilterClassCode ?? string.Empty, FilterProfessionalText ?? string.Empty);
                OnPropertyChanged(nameof(VisibleCollectionsList));
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message, Loc.Tr("Error", "Erro")));
        }
        finally
        {
            DeletedCollectionsListIsLoading = false;
        }
    }

    /// <summary>Item cujo menu de aÃ§Ãµes (trÃªs pontinhos) foi aberto; usado pelo dropdown para Cancelar deleÃ§Ã£o.</summary>
    [ObservableProperty] private ProfessionalTask pendingItemForCancelDeletion;

    /// <summary>Cancela a solicitaÃ§Ã£o de exclusÃ£o da coleÃ§Ã£o em PendingItemForCancelDeletion (chamado pelo menu dropdown).</summary>
    [RelayCommand]
    public async Task CancelDeleteCollectionFromPendingCommand()
    {
        await CancelDeleteCollectionCommand(PendingItemForCancelDeletion);
    }

    /// <summary>Cancela a solicitaÃ§Ã£o de exclusÃ£o da coleÃ§Ã£o e reintegra na lista normal.</summary>
    [RelayCommand]
    public async Task CancelDeleteCollectionCommand(ProfessionalTask item)
    {
        if (item == null || string.IsNullOrEmpty(item.classCode)) return;
        var confirmed = await GlobalAppStateViewModel.Instance.ShowDialogYesNo(
            Loc.Tr("Do you want to cancel the deletion request for this collection? It will be restored to your recent collections list or expired list, depending on its expiration date.", "Deseja cancelar a solicitaÃ§Ã£o de exclusÃ£o desta coleÃ§Ã£o? Ela voltarÃ¡ para a lista de coleÃ§Ãµes recentes ou para a lista de vencidas, conforme a data de vencimento."),
            Loc.Tr("Cancel deletion", "Cancelar deleÃ§Ã£o"));
        if (!confirmed) return;
        try
        {
            var result = await GlobalAppStateViewModel.lfc.CancelRequestDeleteCollection(item.classCode);
            if (result?.loginFailed == true)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(result.message ?? Loc.Tr("Login failed.", "Falha no login."), Loc.Tr("Error", "Erro"));
                return;
            }
            if (result?.success != true)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(result?.message ?? Loc.Tr("Could not cancel deletion.", "NÃ£o foi possÃ­vel cancelar a deleÃ§Ã£o."), Loc.Tr("Error", "Erro"));
                return;
            }
            await LoadDeletedCollectionsAsync();
            var pt = await GlobalAppStateViewModel.lfc.GetProfessionalTask(item.classCode);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (pt != null)
                {
                    // ColeÃ§Ã£o vencida: data de deleÃ§Ã£o jÃ¡ passou â†’ vai para a lista de vencidas
                    bool isExpired = pt.ScheduledDeletionDate.HasValue && pt.ScheduledDeletionDate.Value <= DateTimeOffset.Now;
                    if (isExpired)
                    {
                        if (!ExpiredCollectionsList.Any(c => c.classCode == pt.classCode))
                            ExpiredCollectionsList.Insert(0, pt);
                        SelectedCollectionsTab = CollectionsTabKind.Expired;
                        SelectedCollection = ExpiredCollectionsList.First(c => c.classCode == pt.classCode);
                    }
                    else
                    {
                        if (!CollectionsList.Any(c => c.classCode == pt.classCode))
                            CollectionsList.Insert(0, pt);
                        SelectedCollectionsTab = CollectionsTabKind.Normal;
                        SelectedCollection = CollectionsList.First(c => c.classCode == pt.classCode);
                    }
                }
                ApplyLocalFilterAllSources(FilterClassCode ?? string.Empty, FilterProfessionalText ?? string.Empty);
                OnPropertyChanged(nameof(VisibleCollectionsList));
                NotifyDeletedCollectionViewState();
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message, Loc.Tr("Error", "Erro")));
        }
    }
    [RelayCommand]
    public async Task CreateCollectionCommand()
    {
        string attempClassCode = TbCollectionName;
        try
        {



            IsCreatingCollection = true;
            
            // Valida o diretÃ³rio de downloads PRIMEIRO, antes de qualquer outra validaÃ§Ã£o
            // Esta validaÃ§Ã£o DEVE bloquear a criaÃ§Ã£o se o diretÃ³rio nÃ£o estiver vÃ¡lido
            var (isValid, errorMessage) = GlobalAppStateViewModel.Instance.ValidateDownloadDirectory();
            if (!isValid)
            {
                // Se o diretÃ³rio nÃ£o estiver vÃ¡lido, mostra o diÃ¡logo e forÃ§a a seleÃ§Ã£o
                await GlobalAppStateViewModel.Instance.ValidateAndPromptDownloadDirectoryIfNeeded();
                
                // Verifica NOVAMENTE apÃ³s o diÃ¡logo - se ainda nÃ£o estiver vÃ¡lido, BLOQUEIA
                (isValid, errorMessage) = GlobalAppStateViewModel.Instance.ValidateDownloadDirectory();
                if (!isValid)
                {
                    // Se ainda nÃ£o estiver vÃ¡lido apÃ³s o diÃ¡logo, mostra erro e bloqueia a criaÃ§Ã£o
                    // Isso garante que a coleÃ§Ã£o NUNCA serÃ¡ criada sem um diretÃ³rio vÃ¡lido
                    GlobalAppStateViewModel.Instance.ShowDialogOk(errorMessage + "\n\n" + "NÃ£o Ã© possÃ­vel criar a coleÃ§Ã£o sem um diretÃ³rio de downloads vÃ¡lido.");
                    return;
                }
            }
            
            if (string.IsNullOrWhiteSpace(TbCollectionName))
            {
                ValidateCollectionName();
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Collection ID is required", "O ID da coleção é obrigatório."));
                return;
            }
            if ((TbCollectionName?.Trim().Length ?? 0) < MinCollectionIdLength)
            {
                ValidateCollectionName();
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Collection ID must have at least 3 characters", "O ID da coleção deve ter no mínimo 3 caracteres."));
                return;
            }
            if (!IsTextAllowed(TbCollectionName))
            {
                ValidateCollectionName();
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("The class code can only contain letters and numbers"));
                return;
            }
            if (CollectionCreationQueue.Contains(TbCollectionName))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("This class is already being created in the system."));
                return;

            }
            // SÃ³ validar pasta de reconhecimento se nÃ£o for um combo apenas tratamento
            if (!IsTreatmentOnlyCombo && !Directory.Exists(TbRecFolder))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Acknowledgments folder not found"));
                return;
            }
            if (!Directory.Exists(TbEventFolder))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Events folder not found"));
                return;
            }
            if (CheckIfClassAlreadyExists(TbCollectionName))
            {
                var dialog = new ReuploadWarningDialog();
                if (MainWindow.instance != null)
                {
                    await dialog.ShowDialog(MainWindow.instance);
                    if (dialog.Result != true)
                        return;
                }
                else
                {
                    var result = await GlobalAppStateViewModel.Instance.ShowDialogYesNo(Loc.Tr("This contract name already exists...", "This contract name already exists, do you want to update it?"));
                    if(result != true)
                        return;
                }
            }

            CollectionCreationQueue.Enqueue(TbCollectionName);
            
            // CORREÃ‡ÃƒO: Normalizar os paths removendo barras finais para evitar duplicaÃ§Ã£o no backend
            var normalizedEventFolder = TbEventFolder?.TrimEnd('\\', '/') ?? string.Empty;
            var normalizedRecFolder = IsTreatmentOnlyCombo ? string.Empty : (TbRecFolder?.TrimEnd('\\', '/') ?? string.Empty);
            
            ProfessionalTask pt = new ProfessionalTask()
            {
                professionalLogin = CurrentProfessionalName,
                originalClassFolder = GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles,
                classCode = TbCollectionName,
                UploadOnTestSystem = CbUploadOnTestSystem ?? false,
                companyUsername = GlobalAppStateViewModel.lfc.loginResult.User.company,
                originalEventsFolder = normalizedEventFolder,
                originalRecFolder = normalizedRecFolder,
                EnableFaceRelevanceDetection = CbEnableAutoExclusion,
                AutoTreatment = CbEnableAutoTreatment,
                UploadPhotosAreAlreadySorted = CbUploadedPhotosAreAlreadySorted,
                AllowCPFsToSeeAllPhotos = CbAllowCPFsToSeeAllPhotos,
                UploadHD = CbHDBackup,
                UploadComplete = false,
                Description = string.IsNullOrWhiteSpace(TbProfessionalTaskDescription) ? null : TbProfessionalTaskDescription.Trim(),
                EnablePhotosSales = CbEnablePhotoSales,
                PhotosCannotHaveWatermarks = CbPhotosCannotHaveWatermarks,
                PricePerPhotoForSellingOnlineInCents = ConvertDecimalToCents(TbPricePerPhotoForSellingOnline),
                TotalPhotosForFreePerGraduate = (int)(TbTotalPhotosForFreePerGraduate ?? 0.0),
                OCR = CbOcr,
                AllowDeletedProductionToBeFoundAnyone = CbAllowDeletedProductionToBeFoundAnyone,

                 IsTreatmentOnly = IsTreatmentOnlyCombo,
            };
            if (CbEnableAutoTreatment == true)
            {
                pt.AutoTreatmentVersion = "2.0";
            }

            // Definir data de deleÃ§Ã£o agendada baseada na opÃ§Ã£o de armazenamento HD selecionada
            if (CbHDBackup == true && SelectedHdStorageDate.HasValue)
            {
                // Armazenar a data de deleÃ§Ã£o com 5 horas a mais, para manter 05:00 em vez de 00:00
                pt.ScheduledDeletionDate = SelectedHdStorageDate.Value.ToUniversalTime().AddHours(5);
            }

            if (!FileHelper.TryGetFilesWithExtensionsAndFilters(pt.originalEventsFolder, out var eventFiles, out var deniedEventsPath, out var eventsErr))
            {
                if (eventsErr is UnauthorizedAccessException)
                    await GlobalAppStateViewModel.Instance.ShowFolderAccessDeniedDialogAsync(pt.originalEventsFolder, deniedEventsPath ?? pt.originalEventsFolder, eventsErr);
                else
                    GlobalAppStateViewModel.Instance.ShowDialogOk($"NÃ£o foi possÃ­vel ler as fotos da pasta de eventos.\n\n{eventsErr?.Message}");
                return;
            }

            List<FileInfo> recFiles;
            string deniedRecPath = null;
            Exception recErr = null;
            if (IsTreatmentOnlyCombo)
            {
                recFiles = new List<FileInfo>();
            }
            else if (!FileHelper.TryGetFilesWithExtensionsAndFilters(pt.originalRecFolder, out recFiles, out deniedRecPath, out recErr))
            {
                if (recErr is UnauthorizedAccessException)
                    await GlobalAppStateViewModel.Instance.ShowFolderAccessDeniedDialogAsync(pt.originalRecFolder, deniedRecPath ?? pt.originalRecFolder, recErr);
                else
                    GlobalAppStateViewModel.Instance.ShowDialogOk($"NÃ£o foi possÃ­vel ler as fotos da pasta de reconhecimentos.\n\n{recErr?.Message}");
                return;
            }
            var checkIfClassCanBeCreated = CheckIfClassCanBeCreated(pt, eventFiles, recFiles);
            if (checkIfClassCanBeCreated.response == false)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(checkIfClassCanBeCreated.message);
                return;
            }

            var graduatesDataToUpload = GraduatesData.ToList();
            graduatesDataToUpload.RemoveAll(x => x.CPF == "" || x.CPF == null);

            if (graduatesDataToUpload.Count > 0)
            {
                pt.PhotosDistribution = true;
                // this is a double check. The UI already prevents this case from happening by setting the checkbox to true and disabling it when the GraduatesData collection changes.
                // Check GraduatesData_CollectionChanged
                pt.UploadHD = true; // in order to distribute photos with decent quality, UploadHD is set to true whenever there are CPFs.
            }
            else
            {
                pt.PhotosDistribution = false;
                //pt.UploadHD = false; -> this line was removed when implementing the change to let users make backups without CPFs.
            }

            if (_preConfiguredComboAuthority != null)
                PreConfiguredComboProfessionalTaskAuthority.Apply(_preConfiguredComboAuthority, pt);

            // CORREÃ‡ÃƒO: Normalizar os paths base removendo barra final para garantir que shortPaths comecem com \
            // O Beta nÃ£o tem barra final no originalEventsFolder, entÃ£o os shortPaths ficam com \ no inÃ­cio
            // O Alpha tinha barra final, entÃ£o os shortPaths ficavam sem \ no inÃ­cio, causando duplicaÃ§Ã£o no backend
            var eventsBase = pt.originalEventsFolder.TrimEnd('\\', '/');
            var recBase = string.IsNullOrEmpty(pt.originalRecFolder) ? "" : pt.originalRecFolder.TrimEnd('\\', '/');
            
            var eventFilesShortPaths = eventFiles.Select(x => x.FullName.Substring(eventsBase.Length)).ToList();
            var recFilesShortPaths = string.IsNullOrEmpty(recBase) 
                ? new List<string>() 
                : recFiles.Select(x => x.FullName.Substring(recBase.Length)).ToList();

            // Impedir criar coleÃ§Ã£o se existirem fotos com o mesmo nome em Eventos e Reconhecimentos
            // Se o usuÃ¡rio corrigir os nomes, ao clicar em "JÃ¡ corrigi, continuar" recarregamos os arquivos e repetimos a verificaÃ§Ã£o.
            while (true)
            {
                var duplicates = GetDuplicatePhotoNamesBetweenEventsAndRec(eventsBase, recBase, eventFilesShortPaths, recFilesShortPaths);
                if (duplicates.Count == 0)
                    break;

                const int maxShow = 15;
                var intro = "Existem fotos com o mesmo nome na pasta de Eventos e na pasta de Reconhecimentos. " +
                    "Para evitar conflitos, renomeie as fotos duplicadas (em uma das pastas) e tente novamente." +
                    "\n\nâ€” Fotos com o mesmo nome â€”";
                var moreCount = duplicates.Count > maxShow ? duplicates.Count - maxShow : 0;

                var dialog = new DuplicatePhotosWarningWindow();
                dialog.SetContent(intro, duplicates, maxShow, moreCount);

                var owner = MainWindow.instance;
                await (owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog(null!));

                if (!dialog.ContinueRequested)
                    return;

                // Recarrega as pastas para refletir os renomeios feitos durante o diÃ¡logo
                if (!FileHelper.TryGetFilesWithExtensionsAndFilters(pt.originalEventsFolder, out eventFiles, out deniedEventsPath, out eventsErr))
                {
                    if (eventsErr is UnauthorizedAccessException)
                        await GlobalAppStateViewModel.Instance.ShowFolderAccessDeniedDialogAsync(pt.originalEventsFolder, deniedEventsPath ?? pt.originalEventsFolder, eventsErr);
                    else
                        GlobalAppStateViewModel.Instance.ShowDialogOk($"NÃ£o foi possÃ­vel reler as fotos da pasta de eventos.\n\n{eventsErr?.Message}");
                    return;
                }

                if (IsTreatmentOnlyCombo)
                {
                    recFiles = new List<FileInfo>();
                }
                else if (!FileHelper.TryGetFilesWithExtensionsAndFilters(pt.originalRecFolder, out recFiles, out deniedRecPath, out recErr))
                {
                    if (recErr is UnauthorizedAccessException)
                        await GlobalAppStateViewModel.Instance.ShowFolderAccessDeniedDialogAsync(pt.originalRecFolder, deniedRecPath ?? pt.originalRecFolder, recErr);
                    else
                        GlobalAppStateViewModel.Instance.ShowDialogOk($"NÃ£o foi possÃ­vel reler as fotos da pasta de reconhecimentos.\n\n{recErr?.Message}");
                    return;
                }

                var checkIfClassCanBeCreatedAgain = CheckIfClassCanBeCreated(pt, eventFiles, recFiles);
                if (checkIfClassCanBeCreatedAgain.response == false)
                {
                    GlobalAppStateViewModel.Instance.ShowDialogOk(checkIfClassCanBeCreatedAgain.message);
                    return;
                }

                eventFilesShortPaths = eventFiles.Select(x => x.FullName.Substring(eventsBase.Length)).ToList();
                recFilesShortPaths = string.IsNullOrEmpty(recBase)
                    ? new List<string>()
                    : recFiles.Select(x => x.FullName.Substring(recBase.Length)).ToList();
            }

            bool shouldNotifyPipedriveAboutFirstUse = false;
            bool shouldNotifyPipedriveAboutFreeTrial50PercentReached = false;
            bool shouldNotifyPipedriveAboutFreeTrialLimitReached = false;
            if (RemainingFreeTrialPhotosResult.IsFreeTrialActive)
            {
                //VERIFICA SE ï¿½ O PRIMEIRO USO DO SISTEMA
                if(RemainingFreeTrialPhotosResult.IsFirstUse)
                    shouldNotifyPipedriveAboutFirstUse = true;

                int totalCollectionPhotos = recFilesShortPaths.Count + eventFilesShortPaths.Count;
                // Em reupload, a cota do teste deve considerar apenas as fotos NOVAS (diferenÃ§a vs. o que jÃ¡ existe na coleÃ§Ã£o).
                int existingCollectionPhotos = 0;
                if (IsReupload && SelectedCollection != null)
                    existingCollectionPhotos = (SelectedCollection.recognitionPhotos ?? 0) + (SelectedCollection.eventPhotos ?? 0);
                int photosToConsumeFromFreeTrial = IsReupload
                    ? Math.Max(0, totalCollectionPhotos - existingCollectionPhotos)
                    : totalCollectionPhotos;

                //VERIFICA SE A QUOTA DE TESTE ESTï¿½ PASSANDO DE 50%
                if(RemainingFreeTrialPhotosResult.HalfQuotaRemainingPhotos > 0 && photosToConsumeFromFreeTrial > RemainingFreeTrialPhotosResult.HalfQuotaRemainingPhotos)
                    shouldNotifyPipedriveAboutFreeTrial50PercentReached = true;

                //VERIFICA SE A QUOTA DE TESTE ESTï¿½ SENDO EXCEDIDA
                if (RemainingFreeTrialPhotosResult.RemainingFreeTrialPhotos > 0
                    && photosToConsumeFromFreeTrial > 0
                    && photosToConsumeFromFreeTrial > RemainingFreeTrialPhotosResult.RemainingFreeTrialPhotos)
                {
                    shouldNotifyPipedriveAboutFreeTrialLimitReached = true;
                    var msg = $"**VocÃª atingiu o limite de fotos grÃ¡tis do teste.**\n\n" +
                              $"Neste envio, **{photosToConsumeFromFreeTrial}** foto(s) serÃ£o contabilizadas na cota grÃ¡tis.\n" +
                              $"Saldo grÃ¡tis disponÃ­vel: **{RemainingFreeTrialPhotosResult.RemainingFreeTrialPhotos}** foto(s).\n\n" +
                              $"A partir de agora, as fotos adicionais desta turma (e de turmas futuras) poderÃ£o estar sujeitas a cobranÃ§a.";
                    await GlobalAppStateViewModel.Instance.ShowDialogEntendi(msg, "Limite de fotos grÃ¡tis do teste atingido");
                }
            }

            var r = await GlobalAppStateViewModel.lfc.UpdateOrCreateProfessionalTaskAsync(pt, recFilesShortPaths, eventFilesShortPaths);
            if (r != null && r.success)
            {
                foreach (var g in graduatesDataToUpload)
                {
                    g.ClassCode = pt.classCode;
                    g.Company = pt.companyUsername;
                }
                if (graduatesDataToUpload.Count > 0)
                    await GlobalAppStateViewModel.lfc.RegisterGraduatesCPFsAndEmails(graduatesDataToUpload);

                Action<int> callback = MainWindowViewModel.Instance != null
                    ? MainWindowViewModel.Instance.UpdateProgressBarUpdateComponent
                    : _ => { };
                App.StartUploadConcurrentApp(pt, callback);

                await UpdateProfessionalTasksList();
                var currentPt = CollectionsListFiltered.FirstOrDefault(x => x.classCode == pt.classCode);
                if (currentPt != null)
                    SelectedCollection = currentPt;
                ActiveComponent = ActiveViews.CollectionView;

                if(RemainingFreeTrialPhotosResult.IsFreeTrialActive == true)
                    GetInfosAboutFreeTrialPeriod();

            }
            else
            {
                await GlobalAppStateViewModel.Instance.ShowCollectionCreationSupportDialogAsync(r?.message);
            }

            // PipeDrive notifications are now handled server-side

        }
        catch(Exception ex)
        {
            //var bbox = MessageBoxManager
            //    .GetMessageBoxStandard("", $"{ex.Message} | {ex.StackTrace}");
            //var result = bbox.ShowWindowDialogAsync(MainWindow.instance);

            GlobalAppStateViewModel.Instance.ShowDialogOk($"{ex.Message}");
        }
        finally
        {
            CollectionCreationQueue.TryDequeue(out attempClassCode!); // ! instruction for silence compilator becase warning not null here
            IsCreatingCollection = false;

            // Atualiza mensagens do servidor (ex.: limite atingido, erro de cota) sem abrir o componente
            if (MainWindowViewModel.Instance != null)
                _ = MainWindowViewModel.Instance.LoadUserMessagesAsync(forceReload: true);
        }
    }

    /// <summary>
    /// Devolve as fotos que tÃªm o mesmo nome na pasta de Eventos e na de Reconhecimentos (comparaÃ§Ã£o case-insensitive).
    /// Cada entrada contÃ©m o nome do ficheiro e os caminhos completos em cada pasta para exibir ao utilizador.
    /// </summary>
    private static List<(string fileName, string eventFullPath, string recFullPath)> GetDuplicatePhotoNamesBetweenEventsAndRec(
        string eventsBase,
        string recBase,
        List<string> eventFilesShortPaths,
        List<string> recFilesShortPaths)
    {
        if (string.IsNullOrEmpty(recBase) || recFilesShortPaths.Count == 0)
            return new List<(string, string, string)>();

        var recByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in recFilesShortPaths)
        {
            var fn = Path.GetFileName(p);
            if (!string.IsNullOrEmpty(fn))
                recByFileName[fn] = Path.Combine(recBase, p.TrimStart('\\', '/'));
        }

        var duplicates = new List<(string fileName, string eventFullPath, string recFullPath)>();
        foreach (var eventShort in eventFilesShortPaths)
        {
            var fn = Path.GetFileName(eventShort);
            if (string.IsNullOrEmpty(fn) || !recByFileName.TryGetValue(fn, out var recFullPath))
                continue;
            var eventFullPath = Path.Combine(eventsBase, eventShort.TrimStart('\\', '/'));
            duplicates.Add((fn, eventFullPath, recFullPath));
        }

        return duplicates;
    }

    /// <summary>
    /// Escreve uma mensagem de erro no arquivo log.txt
    /// </summary>
    private void WriteToLogFile(string message)
    {
        try
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
            File.AppendAllText(logFilePath, logMessage);
        }
        catch (Exception ex)
        {
            // Se nÃ£o conseguir escrever no log, apenas ignora silenciosamente
            System.Diagnostics.Debug.WriteLine($"Erro ao escrever no log: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica se o total de fotos da coleÃ§Ã£o corresponde ao total de fotos reconhecidas no servidor.
    /// Se houver divergÃªncia, atualiza a contagem de fotos via endpoint UpdateCollectionPhotoCount.
    /// </summary>
    /// <returns>True se pode prosseguir com a operaÃ§Ã£o, False se houve erro na atualizaÃ§Ã£o</returns>
    private async Task<bool> VerifyAndUpdatePhotoCountIfNeeded()
    {
        if (SelectedCollection == null || ServerProgressValues == null)
            return true;

        // Total de fotos da coleÃ§Ã£o (somatÃ³rio de fotos de reconhecimento + eventos)
        int collectionTotalPhotos = (SelectedCollection.recognitionPhotos ?? 0) + (SelectedCollection.eventPhotos ?? 0);
        
        // Total de fotos do reconhecimento no servidor (ServerProgress.total)
        int serverTotalPhotos = ServerProgressValues.total ?? 0;

        // Se os valores sÃ£o iguais, segue o fluxo normal
        if (collectionTotalPhotos == serverTotalPhotos)
            return true;

        // Valores diferentes - precisa atualizar a contagem de fotos
        try
        {
            var result = await GlobalAppStateViewModel.lfc.UpdateCollectionPhotoCount(SelectedCollection.classCode);
            
            if (result != null && result.success)
            {
                // AtualizaÃ§Ã£o bem-sucedida - atualiza a lista de coleÃ§Ãµes para refletir as mudanÃ§as
                var classCodeToUpdate = SelectedCollection.classCode;
                await UpdateProfessionalTasksList();
                
                // Restaurar a coleÃ§Ã£o selecionada apÃ³s atualizar a lista
                if (!string.IsNullOrEmpty(classCodeToUpdate))
                {
                    var updatedCollection = CollectionsListFiltered.FirstOrDefault(x => x.classCode == classCodeToUpdate);
                    if (updatedCollection != null)
                    {
                        SelectedCollection = updatedCollection;
                    }
                }
                
                // Atualiza os valores locais e as barras de progresso
                await UpdateProgressBars();
                return true;
            }
            else
            {
                // NÃ£o foi possÃ­vel atualizar a contagem - registra no log
                var message = result?.message;
                if (string.IsNullOrEmpty(message))
                {
                    message = "NÃ£o foi possÃ­vel sincronizar a contagem de fotos automaticamente.";
                }
                
                WriteToLogFile($"VerifyAndUpdatePhotoCountIfNeeded - Falha ao atualizar contagem: {message}");
                return true; // Continua o fluxo mesmo se nÃ£o conseguiu atualizar
            }
        }
        catch (Exception ex)
        {
            // Em caso de exceÃ§Ã£o, registra no log
            WriteToLogFile($"VerifyAndUpdatePhotoCountIfNeeded - ExceÃ§Ã£o: {ex.Message}\nStackTrace: {ex.StackTrace}");
            return true; // Continua o fluxo mesmo em caso de erro
        }
    }

    /// <summary>
    /// Retorna a pasta Save para usar no TagSort (baixar separacao.hermes da nuvem).
    /// Se o usuÃ¡rio configurou diretÃ³rio de downloads diferente de Documents, usa esse path para nÃ£o criar pasta em Documents antes do Download Manager rodar.
    /// </summary>
    private static string GetSaveFolderForTagSort(string classCode)
    {
        var opts = GlobalAppStateViewModel.options?.DefaultPathToDownloadProfessionalTaskFiles;
        var docsPath = SharedClientSide.Helpers.Constants.SeparationFolder.FullName.TrimEnd('\\', '/');
        if (!string.IsNullOrWhiteSpace(opts) && Directory.Exists(opts))
        {
            var optsNorm = System.IO.Path.GetFullPath(opts.TrimEnd('\\', '/'));
            var docsNorm = System.IO.Path.GetFullPath(docsPath);
            if (!string.Equals(optsNorm, docsNorm, StringComparison.OrdinalIgnoreCase))
                return System.IO.Path.Combine(optsNorm, classCode, "Save");
        }
        return SharedClientSide.Helpers.Constants.GetSaveFolder(classCode);
    }

    [RelayCommand]
    public async Task TagSortCommand() 
    {
        try
        {
            SharedClientSide.Helpers.AppInstaller.MsixLog("TagSortCommand INICIADO");
            // ValidaÃ§Ã£o: verificar se SelectedCollection nÃ£o Ã© null antes de usar
            if (SelectedCollection == null)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Nenhuma coleÃ§Ã£o selecionada. Por favor, selecione uma coleÃ§Ã£o antes de usar Tag/Sort.");
                return;
            }

            BtTagSortIsRunning = true;

            // Verificar se o total de fotos da coleÃ§Ã£o corresponde ao total de fotos reconhecidas no servidor
            if (!await VerifyAndUpdatePhotoCountIfNeeded())
                return;

            if (SelectedSeparationFile != null)
            {
                BtTagSortIsEnabled = false;
                // Usar o path das opÃ§Ãµes quando o usuÃ¡rio escolheu diretÃ³rio diferente de Documents, para nÃ£o criar Save em Documents antes do Download Manager rodar
                var saveFolderPath = GetSaveFolderForTagSort(SelectedCollection.classCode);
                var sepFile = new FileInfo(Path.Combine(saveFolderPath, "separacao.hermes"));

                if (SelectedSeparationFile.FileLocationType == SharedClientSide.ClassSeparationFile.FileLocationTypes.CLOUD)
                {
                    if (sepFile.Exists)
                    {
                        var msgBoxResult = await GlobalAppStateViewModel.Instance.ShowDialogYesNo(Loc.Tr("You're about to open a file from the cloud...", Loc.Tr("Confirmation")));
                        if (msgBoxResult != true)
                            return;
                    }
                    var classSeparationFile = SelectedSeparationFile;

                    var ur = await LesserFunctionClient.DefaultClient.GetBlobBytesFromUserFolderInClassesContainer(classSeparationFile.FilePathInCloudCompanyFolder, classSeparationFile.StorageLocation);
                    if (ur != null && ur.success)
                    {
                        var bytes = ur.Content;
                        if(sepFile.Directory == null)
                            return;
                        if (sepFile.Directory.Exists == false)
                            sepFile.Directory.Create();
                        SeparationFileSharedManagement.performBackup(SeparationFileSharedManagement.GetSeparationDocumentPath(SelectedCollection.classCode), SeparationFileSharedManagement.GetBackupsFolderPath(SelectedCollection.classCode));
                        File.WriteAllBytes(sepFile.FullName, bytes);
                    }

                    var ur2 = await LesserFunctionClient.DefaultClient.GetBlobBytesFromUserFolderInClassesContainer(classSeparationFile.SeparationProgressPathCloudCompanyFolder, classSeparationFile.StorageLocation);
                    if (ur2 != null && ur2.success)
                    {
                        var localProgressFile = new FileInfo(Path.Combine(saveFolderPath, "separationProgress.txt"));
                        File.WriteAllBytes(localProgressFile.FullName, ur2.Content);
                    }
                }
            }
            Action<int> callback = MainWindowViewModel.Instance != null
                ? MainWindowViewModel.Instance.UpdateProgressBarUpdateComponent
                : _ => { };
            SharedClientSide.Helpers.AppInstaller.MsixLog("TagSortCommand chamando App.StartDownloadApp");
            App.StartDownloadApp(SelectedCollection, callback);
        }
        catch(Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message + ex.StackTrace);
        }
        finally
        {
            await Task.Delay(500);
            BtTagSortIsEnabled = true;
            BtTagSortIsRunning = false;
        }
    }
    [RelayCommand]
    public async Task ExportCommand()
    {
        try
        {
            BtExportIsRunning = true;
            BtExportIsEnabled = false;
            await Task.Delay(200);
            if (!Directory.Exists(AppInstaller.AppRootFolder))
                Directory.CreateDirectory(AppInstaller.AppRootFolder);
            File.WriteAllText(AppInstaller.ClassToExportTxtFilePath, JsonConvert.SerializeObject(SelectedCollection, Formatting.Indented));
            File.WriteAllText(AppInstaller.ClassSeparationFileTxtFilePath, JsonConvert.SerializeObject(SelectedSeparationFile, Formatting.Indented));

            Action<int> callback = MainWindowViewModel.Instance != null
                ? MainWindowViewModel.Instance.UpdateProgressBarUpdateComponent
                : _ => { };
            App.StartOrganizeApp(callback);
        }
        catch
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Nï¿½o foi possï¿½vel preencher os dados automaticamente. " +
                "Serï¿½ necessï¿½rio inserir manualmente o nome da pasta e inserir o arquivo separacao.hermes no local correto manualmente.");
        }
        finally
        {
            // Delay para permitir que o InstallerRunner inicie antes de reabilitar o botÃ£o
            await Task.Delay(500);
            BtExportIsEnabled = true;
            BtExportIsRunning = false;
        }
    }
    [RelayCommand]
    public async Task DownloadHDCommand()
    {
        try
        {
            BtDownloadHdIsEnabled = false;
            BtDownloadHdIsRunning = true;
            if (!Directory.Exists(AppInstaller.AppRootFolder))
                Directory.CreateDirectory(AppInstaller.AppRootFolder);
            File.WriteAllText(AppInstaller.ClassToDownloadHDTxtFilePath, JsonConvert.SerializeObject(SelectedCollection, Formatting.Indented));

            Action<int> callback = MainWindowViewModel.Instance != null
                ? MainWindowViewModel.Instance.UpdateProgressBarUpdateComponent
                : _ => { };
            
            // Usar InstallerRunner para executar em background e evitar travada da UI
            LesserDashboardClient.Helpers.InstallerRunner.RunInBackground(
                appName: "download-hd",
                onUiProgress: callback,
                args: "",
                onUiDone: () => callback?.Invoke(100),
                onUiError: msg => 
                {
                    GlobalAppStateViewModel.Instance.ShowDialogOk($"Erro na instalaÃ§Ã£o: {msg}");
                }
            );
        }
        catch (Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message + ex.StackTrace);
        }
        finally
        {
            // Delay para permitir que o InstallerRunner inicie antes de reabilitar o botÃ£o
            await Task.Delay(500);
            BtDownloadHdIsEnabled = true;
            BtDownloadHdIsRunning = false;
        }
    }
    [RelayCommand]
    public async Task ReEnqueueCommand()
    {
        try
        {
            BtReEnqueueIsEnabled = false;
            BtReenqueueIsRunning = true;
            var r = await GlobalAppStateViewModel.lfc.ReenqueueClassHttpTrigger(SelectedCollection.classCode);
            if (!string.IsNullOrEmpty(r.message))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(r.message);
            }
        }
        catch
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Ocorreu um erro ao colocar as imagens na fila para reconhecimento novamente.");
        }
        finally
        {
            BtReEnqueueIsEnabled = true;
            BtReenqueueIsRunning = false;
        }
    }
    [RelayCommand]
    public async Task RequestManualTreatmentAllCollectionCommand()
    {
        try
        {
            BtRequestManualTreatmentAllCollectionIsEnabled = false;
            BtRequestManualTreatmentAllCollectionIsRunning = true;
            bool dialog = await GlobalAppStateViewModel.Instance.ShowDialogYesNo("Tem certeza que deseja solicitar o tratamento manual de todas as imagens da sua coleï¿½ï¿½o? \n Aï¿½ï¿½o Irreversï¿½vel", "Atenï¿½ï¿½o");

            if (dialog == true)
            {
                var r = await GlobalAppStateViewModel.lfc.RequestTreatmentOfEntireCollection(SelectedCollection.classCode);
            }
        }
        catch
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Ocorreu um erro ao colocar as imagens na fila para reconhecimento novamente.");
        }
        finally
        {
            BtRequestManualTreatmentAllCollectionIsEnabled = true;
            BtRequestManualTreatmentAllCollectionIsRunning = false;
        }
    }
    [RelayCommand]
    public void InsertDataBasedOnRecFolderCommand()
    {
        // Se for apenas tratamento, nÃ£o buscar dados de reconhecimento
        if (IsTreatmentOnlyCombo)
        {
            return;
        }

        // Verificar se pode adicionar CPFs (bloqueado para turmas de outro perÃ­odo no reupload)
        if (!CanAddCPFs)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(CPFsErrorMessage);
            return;
        }
        
        if (Directory.Exists(TbRecFolder) == false)
        {
            //MessageBox.Show("A pasta de reconhecimentos especificada nÃ£o existe.");
            return;
        }
        if (!FileHelper.TryGetFilesWithExtensionsAndFilters(TbRecFolder, out var gradPhotos, out var deniedPath, out var err))
        {
            if (err is UnauthorizedAccessException)
            {
                // Este comando nÃ£o Ã© async; mostrar diÃ¡logo sem bloquear.
                _ = GlobalAppStateViewModel.Instance.ShowFolderAccessDeniedDialogAsync(TbRecFolder, deniedPath ?? TbRecFolder, err);
            }
            else
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk($"NÃ£o foi possÃ­vel ler as fotos da pasta de reconhecimentos.\n\n{err?.Message}");
            }
            return;
        }

        string getShortpathFromImagePath(string fullPath)
        {
            var shortPath = fullPath.Substring(TbRecFolder.Length, fullPath.Length - TbRecFolder.Length);
            shortPath = shortPath.Substring(1, shortPath.Length - 1);
            return shortPath;
        }

        foreach (var im in gradPhotos)
        {
            var shortPath = getShortpathFromImagePath(im.FullName);
            if (GraduatesData.ToList().Find(x => x.ShortPath.ToLower() == shortPath.ToLower()) != null)
                continue;
            GraduatesData.Add(new GraduateByCPF()
            {
                ShortPath = shortPath,
                Name = ""
            });

        }

        for (var i = 0; i < GraduatesData.Count; i++)
        {
            var grad = GraduatesData[i];
            if (gradPhotos.Find(x => getShortpathFromImagePath(x.FullName).ToLower() == grad.ShortPath.ToLower()) == null)
            {
                GraduatesData.RemoveAt(i);
                i--;
            }
        }
        SortGraduatesDataAlphabetically();
    }
    [RelayCommand]
    public async Task GenerateExcelBasedOnDataCommand()
    {
        if (IsTreatmentOnlyCombo)
            return;

        try
        {
            ComponentNewCollectionIsEnabled = false;
            await GenerateAndOpenExcelForGraduatesAsync(GraduatesData, TbRecFolder, CanAddCPFs, CPFsErrorMessage);
            SortGraduatesDataAlphabetically();
        }
        catch (Exception e)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(e.Message);
        }
        finally
        {
            ComponentNewCollectionIsEnabled = true;
        }
    }

    [RelayCommand]
    public void ToggleViewReportsContainer()
    {
        ReportsContainerIsVisible = !ReportsContainerIsVisible;
    }
    [RelayCommand]
    public async Task CancelBillingCommand()
    {
        try 
        {
            CancelBillingIsRunning = true;
            if (SelectedCollection == null)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Selecione uma coleÃ§Ã£o para continuar.");
                return;
            }
            
            if (SelectedCollectionForCancelBilling == null)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Selecione uma item para para continuar.");
                return;
            }

            var repeatedClass = SelectedCollectionForCancelBilling.classCode;
            var result = await GlobalAppStateViewModel.lfc.CheckAndCancelBillingForRepeatedClass(SelectedCollection.classCode + "?" + repeatedClass);
            if (result != null && result.success == false)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(result.message);
            }
            else
            {
                var pt = await GlobalAppStateViewModel.lfc.GetProfessionalTask(SelectedCollection.classCode);
                SelectedCollectionForCancelBilling.BillingCancelled = pt.BillingCancelled;
                FilterProfessionalTasks("","");
                GlobalAppStateViewModel.Instance.ShowDialogOk("Cobranï¿½a do contrato cancelada com sucesso.");
                await OpenSelectProfessionalViewCommand();
            }

        }
        finally
        {
            CancelBillingIsRunning = false;
        }
    }
    [RelayCommand]
    public void GeneralReportCommand()
    {
        try
        {
            GeneratingGeneralReport = true;
            if (SeparationProgressValue == null)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Nï¿½o foram encontrados dados no servidor sobre esta turma, ou houve problema na conexï¿½o. O profissional jï¿½ separou esta turma?");
                return;
            }
            var photographers = SeparationProgressValue.photographers;
            var stringList = new List<string>() { "Dados sobre a turma " + SeparationProgressValue.code, "Fotï¿½grafo,Total de fotos,Fotos aproveitadas" };
            foreach (Photographer p in photographers)
            {
                stringList.Add(p.name + ";" + p.totalPhotos + ";" + p.totalPhotosUtilized);
            }
            string text = string.Join("\r\n", stringList);
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            string fileName = System.IO.Path.GetTempFileName() + ".csv";

            File.WriteAllBytes(fileName, bytes);

            var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true // <- necessï¿½rio para abrir com o aplicativo padrï¿½o
            };
            proc.Start();
        }
        finally
        {
            GeneratingGeneralReport = false;
        }
    }
    [RelayCommand]
    public async Task ReportPerGraduatesCommand()
    {
        try
        {
            GeneratingReportPerGraduate = true;
            var lfc = GlobalAppStateViewModel.lfc;

            string fileAddress = await lfc.GetClassProgressFileAddress(SelectedCollection.classCode, "separacao.hermes");

            using (WebClient wc = new WebClient())
            {
                wc.DownloadDataAsync(new Uri(fileAddress));

                wc.DownloadDataCompleted += (s, ev) =>
                {
                    string str = Encoding.UTF8.GetString(ev.Result);
                    var lines = str.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();


                    List<Graduate> graduates = new List<Graduate>();
                    bool success; string message;
                    var photosLoad = new PhotosInfoLoad();
                    (success, message) = photosLoad.loadGraduatesAndPhotosInfoFromStringList(lines, "", graduates, true);

                    var reports = new Dictionary<string, GraduateReportInfo>();
                    foreach (Graduate g in graduates)
                    {
                        if (g.Deleted)
                            continue;
                        if (!reports.ContainsKey(g.Name)) // in case the user somehow created two graduates with the same name. This has happened before.
                            reports.Add(g.Name, new GraduateReportInfo() { Name = g.Name });
                    }
                    foreach (PhotoInfo pi in photosLoad.photosInfo)
                    {
                        foreach (Tag tag in pi.tags)
                        {
                            if (!reports.ContainsKey(tag.graduate.Name))
                                continue;
                            if (tag.color == "AZUL")
                                reports[tag.graduate.Name].BlueTags++;
                            else
                                reports[tag.graduate.Name].RedTags++;
                        }
                    }
                    var file = new FileInfo(System.IO.Path.GetTempFileName() + ".xlsx");
                    ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                    using (ExcelPackage package = new ExcelPackage())
                    {
                        ExcelWorksheet ws = package.Workbook.Worksheets.Add(Loc.Tr("Report"));
                        ws.SetValue(1, 1, Loc.Tr("Name"));
                        ws.SetValue(1, 2, Loc.Tr("Approved"));
                        ws.SetValue(1, 3, Loc.Tr("Rejected"));
                        ws.SetValue(1, 4, Loc.Tr("Total"));
                        int line = 1;
                        foreach (var g in reports)
                        {

                            line++;
                            var r = g.Value;
                            ws.SetValue(line, 1, r.Name);
                            ws.SetValue(line, 2, r.BlueTags);
                            ws.SetValue(line, 3, r.RedTags);
                            ws.SetValue(line, 4, r.TotalTags);
                        }
                        package.SaveAs(file);

                        var p = new System.Diagnostics.Process();
                        p.StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = file.FullName,
                            UseShellExecute = true // <- necessï¿½rio para abrir com o aplicativo padrï¿½o
                        };
                        p.Start();
                    }
                };
            }
        }
        catch (Exception exception)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(exception.Message);
        }
        finally
        {
            GeneratingReportPerGraduate = false;
        }
    }
    [RelayCommand]
    public void OpenLinkToUpgradePlanCommand()
    {
        try
        {
            var p = new System.Diagnostics.Process();
            p.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.lesser.biz/lesser-system-helpers/pricing-page/index.html",
                UseShellExecute = true // <- necessï¿½rio para abrir com o aplicativo padrï¿½o
            };
            p.Start();
        }
        catch { }
    }

    // Add New Professional Commands
    private AddNewProfessionalDialog? _addNewProfessionalDialog;
    
    [RelayCommand]
    public async Task OpenAddNewProfessionalDialogCommand()
    {
        try
        {
            // Limpar campos e mensagens
            TbNewProfessionalUsername = "";
            TbNewProfessionalEmail = "";
            TbNewProfessionalConfirmEmail = "";
            CreateProfessionalErrorMessage = "";
            CreateProfessionalSuccessMessage = "";
            
            // Criar e mostrar o dialog
            _addNewProfessionalDialog = new Views.Collections.AddNewProfessionalDialog
            {
                DataContext = this
            };
            
            if (MainWindow.instance != null)
            {
                await _addNewProfessionalDialog.ShowDialog(MainWindow.instance);
            }
        }
        catch (Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk($"Erro ao abrir o formulÃ¡rio: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CreateNewProfessionalCommand()
    {
        try
        {
            CreateProfessionalIsRunning = true;
            CreateProfessionalErrorMessage = "";
            CreateProfessionalSuccessMessage = "";

            // ValidaÃ§Ãµes
            if (string.IsNullOrWhiteSpace(TbNewProfessionalUsername) || TbNewProfessionalUsername.Length < 4)
            {
                CreateProfessionalErrorMessage = "Nome de usuÃ¡rio deve ter pelo menos 4 caracteres.";
                return;
            }

            // Validar formato do username
            if (!System.Text.RegularExpressions.Regex.IsMatch(TbNewProfessionalUsername, @"^[a-zA-Z0-9_.-]+$"))
            {
                CreateProfessionalErrorMessage = "Nome de usuÃ¡rio deve conter apenas letras, nÃºmeros, pontos, hÃ­fens e underscore.";
                return;
            }

            // Validar email
            if (string.IsNullOrWhiteSpace(TbNewProfessionalEmail) || !IsValidEmail(TbNewProfessionalEmail))
            {
                CreateProfessionalErrorMessage = "Email invÃ¡lido.";
                return;
            }

            // Validar confirmaÃ§Ã£o de email
            if (TbNewProfessionalEmail.Trim().ToLower() != TbNewProfessionalConfirmEmail.Trim().ToLower())
            {
                CreateProfessionalErrorMessage = "ConfirmaÃ§Ã£o de email nÃ£o confere.";
                return;
            }

            // Obter dados da empresa
            var companyResult = await GlobalAppStateViewModel.lfc.GetCompanyDetails();
            if (companyResult == null || !companyResult.success || companyResult.Content?.company == null)
            {
                CreateProfessionalErrorMessage = "NÃ£o foi possÃ­vel obter dados da empresa.";
                return;
            }

            // Criar objeto Professional
            var professional = new SharedClientSide.ServerInteraction.Users.Professionals.Professional
            {
                username = TbNewProfessionalUsername.Trim(),
                company = companyResult.Content.company,
                contactMail = TbNewProfessionalEmail.Trim().ToLower()
            };

            // Chamar API para registrar profissional
            var result = await GlobalAppStateViewModel.lfc.RegisterProfessional(professional);

            if (result != null && result.success)
            {
                CreateProfessionalSuccessMessage = "Profissional criado com sucesso!";
                
                // Recarregar lista de profissionais
                await LoadProfessionals();
                
                // Selecionar o novo profissional
                var newProf = Professionals.FirstOrDefault(p => p.username == professional.username);
                if (newProf != null)
                {
                    SelectedProfessional = newProf;
                }

                // Fechar dialog apÃ³s 1.5 segundos
                await Task.Delay(1500);
                _addNewProfessionalDialog?.Close();
            }
            else
            {
                CreateProfessionalErrorMessage = result.message ?? "Erro ao criar profissional.";
            }
        }
        catch (Exception ex)
        {
            CreateProfessionalErrorMessage = $"Erro ao criar profissional: {ex.Message}";
            Console.WriteLine($"Erro ao criar profissional: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            CreateProfessionalIsRunning = false;
        }
    }

    [RelayCommand]
    public void CancelAddNewProfessionalCommand()
    {
        _addNewProfessionalDialog?.Close();
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");
            return emailRegex.IsMatch(email);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Verifica se a coleÃ§Ã£o foi criada em um perÃ­odo de faturamento diferente do atual
    /// </summary>
    /// <param name="collection">A coleÃ§Ã£o a ser verificada</param>
    /// <returns>True se a coleÃ§Ã£o Ã© de outro perÃ­odo de faturamento</returns>
    private bool IsCollectionFromDifferentBillingPeriod(ProfessionalTask collection)
    {
        try
        {
            if (collection?.CreationDate == null)
                return false;
                
            var currentDate = DateTime.UtcNow;
            var creationDate = collection.CreationDate.Value.DateTime;
            
            // Verificar se o mÃªs ou ano sÃ£o diferentes (mesma lÃ³gica do backend)
            // Isso garante que perÃ­odos de faturamento diferentes sejam respeitados
            return currentDate.Month != creationDate.Month || currentDate.Year != creationDate.Year;
        }
        catch
        {
            return false;
        }
    }
    
    #endregion

    #region Dynamic Pricing

    /// <summary>
    /// Carrega combos dinamicamente do servidor
    /// </summary>
    private async void LoadDynamicCombos()
    {
        try
        {
            // Verificar se o cliente estÃ¡ inicializado
            if (GlobalAppStateViewModel.lfc == null)
            {
                // Aguardar um pouco e tentar novamente
                await Task.Delay(2000);
                if (GlobalAppStateViewModel.lfc == null)
                {
                    LoadStaticCombos();
                    return;
                }
            }
            
            // Carregar combos dinamicamente
            var serverCombos = await ComboPriceService.GetDynamicCombosAsync();
            
            // Notificar mudanÃ§as na UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Limpar combos antigos
                DynamicCombos.Clear();
                
                // Adicionar novos combos
                foreach (var combo in serverCombos)
                {
                    DynamicCombos.Add(combo);
                }
                
                // Notificar que a coleÃ§Ã£o mudou
                OnPropertyChanged(nameof(DynamicCombos));
                OnPropertyChanged(nameof(HasCombos));
                OnPropertyChanged(nameof(NoCombosAvailable));
            });
        }
        catch (Exception ex)
        {
            // Em caso de erro, usar combos estÃ¡ticos
            LoadStaticCombos();
        }
    }

    /// <summary>
    /// Carrega os combos estÃ¡ticos como fallback
    /// </summary>
    private void LoadStaticCombos()
    {
        DynamicCombos.Clear();
        
        // Adicionar os combos estÃ¡ticos
        DynamicCombos.Add(NewCollection_Combo0);
        DynamicCombos.Add(NewCollection_Combo1);
        DynamicCombos.Add(NewCollection_Combo2);
        DynamicCombos.Add(NewCollection_Combo3);
        DynamicCombos.Add(NewCollection_Combo4);
        DynamicCombos.Add(NewCollection_Combo5);
        DynamicCombos.Add(NewCollection_Combo6);
        
        OnPropertyChanged(nameof(DynamicCombos));
        OnPropertyChanged(nameof(HasCombos));
        OnPropertyChanged(nameof(NoCombosAvailable));
    }

    /// <summary>
    /// Recarrega os combos dinÃ¢micos (Ãºtil para refresh manual)
    /// </summary>
    [RelayCommand]
    public void RefreshPrices()
    {
        ComboPriceService.ClearCache();
        LoadDynamicCombos();
    }

    [RelayCommand]
    public async Task LoadMoreTasksCommand()
    {
        await LoadAllTasksFromLastFiveYears();
    }

    #endregion
}



