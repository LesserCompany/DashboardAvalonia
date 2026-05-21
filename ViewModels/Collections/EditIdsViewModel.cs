using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedClientSide.Helpers;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Graduate;
using SharedClientSide.ServerInteraction.Users.Professionals;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Collections;

public enum EditIdRegistrationStatus
{
    None,
    Success,
    Failed
}

public partial class EditIdItemStatus : ObservableObject
{
    [ObservableProperty] private EditIdRegistrationStatus status = EditIdRegistrationStatus.None;
    [ObservableProperty] private string failureReason = "";

    public string StatusIcon => Status switch
    {
        EditIdRegistrationStatus.Success => "\u2714",
        EditIdRegistrationStatus.Failed => "\u2716",
        _ => ""
    };

    public IBrush StatusColor => Status switch
    {
        EditIdRegistrationStatus.Success => Brushes.Green,
        EditIdRegistrationStatus.Failed => Brushes.Red,
        _ => Brushes.Transparent
    };

    partial void OnStatusChanged(EditIdRegistrationStatus value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
    }
}

public partial class EditIdsViewModel : ObservableObject
{
    private readonly LesserFunctionClient _lfc;
    private readonly ProfessionalTask _collection;
    private readonly string _recFolder;

    public EditIdsViewModel(
        LesserFunctionClient lfc,
        ProfessionalTask collection,
        string currentProfessionalName,
        string? recFolder,
        bool canEditCpfs,
        string? cpfsErrorMessage)
    {
        _lfc = lfc ?? throw new ArgumentNullException(nameof(lfc));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));

        CurrentProfessionalName = currentProfessionalName ?? "";
        TbCollectionName = _collection.classCode ?? "";
        _recFolder = recFolder ?? "";
        CanEditCPFs = canEditCpfs;
        CPFsErrorMessage = cpfsErrorMessage ?? "";
    }

    public ObservableCollection<string> BlockTypeOptions { get; } =
        new ObservableCollection<string> { GraduateByCPF.BlockTypes.WATERMARK, GraduateByCPF.BlockTypes.ACCESS_DENIED };

    [ObservableProperty] private string tbCollectionName = "";
    [ObservableProperty] private string currentProfessionalName = "";
    [ObservableProperty] private bool canEditCPFs = true;
    [ObservableProperty] private string cPFsErrorMessage = "";

    public string RecFolderForExcel => _recFolder;

    public ObservableCollection<GraduateByCPFWithPhotos> GraduatesData { get; } = new();

    public Dictionary<string, EditIdItemStatus> ItemStatuses { get; } = new(StringComparer.OrdinalIgnoreCase);

    public EditIdItemStatus GetItemStatus(string shortPath)
    {
        var key = NormalizeShortPath(shortPath);
        if (string.IsNullOrEmpty(key)) return new EditIdItemStatus();
        if (!ItemStatuses.TryGetValue(key, out var status))
        {
            status = new EditIdItemStatus();
            ItemStatuses[key] = status;
        }
        return status;
    }

    [ObservableProperty] private bool isRegistering;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private int registerTotalRequests;
    [ObservableProperty] private int registerCompletedRequests;
    [ObservableProperty] private int registerSuccessCount;

    public double RegisterProgressPercent =>
        RegisterTotalRequests <= 0 ? 0 : (double)RegisterCompletedRequests / RegisterTotalRequests * 100.0;

    public string RegisterProgressText =>
        RegisterTotalRequests <= 0
            ? ""
            : $"{RegisterProgressPercent:0}% ({RegisterCompletedRequests}/{RegisterTotalRequests})";

    partial void OnRegisterTotalRequestsChanged(int value)
    {
        OnPropertyChanged(nameof(RegisterProgressPercent));
        OnPropertyChanged(nameof(RegisterProgressText));
    }

    partial void OnRegisterCompletedRequestsChanged(int value)
    {
        OnPropertyChanged(nameof(RegisterProgressPercent));
        OnPropertyChanged(nameof(RegisterProgressText));
    }

    public bool IsGraduatesEditable => !IsLoading && CanEditCPFs;

    public bool CanSubmitEdits => !IsLoading && !IsRegistering && CanEditCPFs;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsGraduatesEditable));
        OnPropertyChanged(nameof(CanSubmitEdits));
    }

    partial void OnIsRegisteringChanged(bool value) => OnPropertyChanged(nameof(CanSubmitEdits));

    partial void OnCanEditCPFsChanged(bool value)
    {
        OnPropertyChanged(nameof(IsGraduatesEditable));
        OnPropertyChanged(nameof(CanSubmitEdits));
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            await LoadGraduatesData();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadGraduatesData()
    {
        GraduatesData.Clear();
        var result = await _lfc.GetGraduatesByCPFByClassCode(_collection.classCode);
        var graduates = result?.Content ?? new();
        foreach (var g in graduates)
            GraduatesData.Add(g);

        MergeRecognitionShortPathsFromRecFolder();
        SortGraduatesDataAlphabetically();
    }

    /// <summary>
    /// Inclui na grade todos os shortpaths de reconhecimento da pasta local da PT,
    /// como se o usuário tivesse selecionado a pasta (mesmo comportamento do New ID no Add IDs).
    /// </summary>
    private void MergeRecognitionShortPathsFromRecFolder()
    {
        if (string.IsNullOrWhiteSpace(_recFolder) || !Directory.Exists(_recFolder))
            return;

        if (!FileHelper.TryGetFilesWithExtensionsAndFilters(_recFolder, out var gradPhotos, out _, out _))
            return;

        var recRoot = _recFolder.TrimEnd('\\', '/');
        var existingShortPaths = new HashSet<string>(
            GraduatesData
                .Where(g => g != null && !string.IsNullOrWhiteSpace(g.ShortPath))
                .Select(g => NormalizeShortPath(g.ShortPath)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in gradPhotos)
        {
            var fullPath = file.FullName;
            if (string.IsNullOrWhiteSpace(fullPath))
                continue;

            string shortPath;
            if (fullPath.StartsWith(recRoot, StringComparison.OrdinalIgnoreCase))
                shortPath = fullPath.Substring(recRoot.Length).TrimStart('\\', '/');
            else
                shortPath = Path.GetFileName(fullPath);

            if (string.IsNullOrWhiteSpace(shortPath))
                continue;

            var normalized = NormalizeShortPath(shortPath);
            if (existingShortPaths.Contains(normalized))
                continue;

            GraduatesData.Add(new GraduateByCPFWithPhotos
            {
                ShortPath = shortPath,
                Name = "",
                Blocked = false,
                BlockType = GraduateByCPF.BlockTypes.WATERMARK
            });
            existingShortPaths.Add(normalized);
        }
    }

    public void SortGraduatesDataAlphabetically()
    {
        if (GraduatesData.Count == 0) return;
        var sorted = GraduatesData.OrderBy(g => g?.ShortPath).ToList();
        GraduatesData.Clear();
        foreach (var g in sorted)
            GraduatesData.Add(g);
    }

    private void ClearItemStatuses()
    {
        ItemStatuses.Clear();
        OnPropertyChanged(nameof(ItemStatuses));
    }

    private void SetItemStatus(string shortPath, EditIdRegistrationStatus status, string failureReason = "")
    {
        var itemStatus = GetItemStatus(shortPath);
        void Apply()
        {
            itemStatus.Status = status;
            itemStatus.FailureReason = failureReason;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Invoke(Apply);
    }

    private string BuildRegisterResultDialogMessage(int ok, int fail)
    {
        if (fail == 0)
            return "IDs atualizados com sucesso.";

        var header = $"IDs atualizados com avisos (ok: {ok}, falhas: {fail}).";
        var failedLines = new List<string>();
        foreach (var kvp in ItemStatuses.Where(x => x.Value.Status == EditIdRegistrationStatus.Failed))
        {
            var key = kvp.Key;
            var cpf = GraduatesData.FirstOrDefault(g => string.Equals(NormalizeShortPath(g?.ShortPath), key, StringComparison.OrdinalIgnoreCase))?.CPF?.Trim();
            var cpfPart = string.IsNullOrWhiteSpace(cpf) ? "" : $" (CPF {cpf})";
            var reason = kvp.Value.FailureReason?.Trim();
            var detail = string.IsNullOrWhiteSpace(reason) ? "" : $" — {reason}";
            failedLines.Add($"• {key}{cpfPart}{detail}");
        }

        if (failedLines.Count == 0)
            return header;

        return header + "\n\n" + string.Join("\n", failedLines);
    }

    private static GraduateByCPF PrepareGraduateForUpload(GraduateByCPFWithPhotos g, string classCode, string company)
    {
        g.ClassCode = classCode;
        g.Company = company;
        g.Blocked ??= false;
        g.BlockType = string.IsNullOrWhiteSpace(g.BlockType) ? GraduateByCPF.BlockTypes.WATERMARK : g.BlockType;
        g.RegistredBy ??= GraduateByCPF.RegistredByTypes.COMPANY;
        return g;
    }

    [RelayCommand]
    public async Task RegisterGraduatesEdits()
    {
        if (_collection == null || string.IsNullOrWhiteSpace(_collection.classCode))
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Selecione uma coleção para continuar.");
            return;
        }

        if (!CanEditCPFs)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(CPFsErrorMessage);
            return;
        }

        var grads = GraduatesData
            .Where(g => g != null && !string.IsNullOrWhiteSpace(g.CPF))
            .Select(g => PrepareGraduateForUpload(g, _collection.classCode, _collection.companyUsername))
            .Cast<GraduateByCPF>()
            .ToList();

        if (grads.Count == 0)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Nenhum ID/CPF para atualizar.");
            return;
        }

        try
        {
            IsRegistering = true;
            RegisterTotalRequests = 0;
            RegisterCompletedRequests = 0;
            RegisterSuccessCount = 0;
            ClearItemStatuses();

            var items = grads
                .Select(g => new
                {
                    Graduate = g,
                    ShortPathRaw = (g.ShortPath ?? "").Trim()
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.ShortPathRaw))
                .ToList();

            if (items.Count == 0)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Nenhum ID/CPF para atualizar.");
                return;
            }

            RegisterTotalRequests = items.Count;

            const int maxParallel = 4;
            using var sem = new SemaphoreSlim(maxParallel, maxParallel);

            async Task<bool> RegisterOneAsync(GraduateByCPF graduate, string shortPathRaw, CancellationToken ct)
            {
                await sem.WaitAsync(ct);
                try
                {
                    var r = await _lfc.RegisterGraduatesCPFsAndEmails(new[] { graduate });
                    var success = r?.loginFailed != true && r?.success == true;
                    if (success)
                    {
                        Interlocked.Increment(ref registerSuccessCount);
                        SetItemStatus(shortPathRaw, EditIdRegistrationStatus.Success);
                    }
                    else
                    {
                        SetItemStatus(shortPathRaw, EditIdRegistrationStatus.Failed, r?.message ?? "Falha na atualização");
                    }
                    return success;
                }
                finally
                {
                    sem.Release();
                    Interlocked.Increment(ref registerCompletedRequests);
                    OnPropertyChanged(nameof(RegisterCompletedRequests));
                    OnPropertyChanged(nameof(RegisterProgressPercent));
                    OnPropertyChanged(nameof(RegisterProgressText));
                }
            }

            using var cts = new CancellationTokenSource();
            var tasks = items.Select(x => RegisterOneAsync(x.Graduate, x.ShortPathRaw, cts.Token)).ToList();
            var results = await Task.WhenAll(tasks);

            var ok = results.Count(r => r);
            var fail = results.Length - ok;

            GlobalAppStateViewModel.Instance.ShowDialogOk(BuildRegisterResultDialogMessage(ok, fail));
            await LoadGraduatesData();
        }
        catch (Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message, "Erro");
        }
        finally
        {
            IsRegistering = false;
        }
    }

    [RelayCommand]
    public async Task GenerateExcelBasedOnDataAsync()
    {
        var collectionsVm = CollectionsViewModel.Instance;
        if (collectionsVm == null)
            return;

        var working = new ObservableCollection<GraduateByCPF>(GraduatesData.Cast<GraduateByCPF>());
        await collectionsVm.GenerateAndOpenExcelForGraduatesAsync(working, _recFolder, CanEditCPFs, CPFsErrorMessage);
        ApplyImportedGraduates(working);
    }

    public string? ImportGraduateDataFromExcelFile(FileInfo f)
    {
        var collectionsVm = CollectionsViewModel.Instance;
        if (collectionsVm == null)
            return "Erro interno.";

        var working = new ObservableCollection<GraduateByCPF>(GraduatesData.Cast<GraduateByCPF>());
        var err = collectionsVm.ImportGraduatesFromExcel(f, working, _recFolder, CanEditCPFs, CPFsErrorMessage);
        if (err != null)
            return err;

        ApplyImportedGraduates(working);
        return null;
    }

    private void ApplyImportedGraduates(ObservableCollection<GraduateByCPF> source)
    {
        GraduatesData.Clear();
        foreach (var g in source)
            GraduatesData.Add(g is GraduateByCPFWithPhotos wp ? wp : new GraduateByCPFWithPhotos(g));
        SortGraduatesDataAlphabetically();
    }

    private static string NormalizeShortPath(string? shortPath)
    {
        if (string.IsNullOrWhiteSpace(shortPath))
            return "";
        return shortPath.Trim().TrimStart('\\', '/').Replace("\\", "/");
    }
}
