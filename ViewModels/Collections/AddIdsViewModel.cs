using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Graduate;
using SharedClientSide.ServerInteraction.Users.Professionals;
using SharedClientSide.ServerInteraction.Users.Requests;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Collections;

public enum AddIdRegistrationStatus
{
    None,
    Success,
    Failed
}

public partial class AddIdItemStatus : ObservableObject
{
    [ObservableProperty] private AddIdRegistrationStatus status = AddIdRegistrationStatus.None;
    [ObservableProperty] private string failureReason = "";

    public string StatusIcon => Status switch
    {
        AddIdRegistrationStatus.Success => "\u2714",
        AddIdRegistrationStatus.Failed => "\u2716",
        _ => ""
    };

    public IBrush StatusColor => Status switch
    {
        AddIdRegistrationStatus.Success => Brushes.Green,
        AddIdRegistrationStatus.Failed => Brushes.Red,
        _ => Brushes.Transparent
    };

    partial void OnStatusChanged(AddIdRegistrationStatus value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
    }
}

public partial class AddIdsViewModel : ObservableObject
{
    private readonly LesserFunctionClient _lfc;
    private readonly ProfessionalTask _collection;

    public AddIdsViewModel(
        LesserFunctionClient lfc,
        ProfessionalTask collection,
        string currentProfessionalName,
        string? recFolder,
        bool canAddCpfs,
        string? cpfsErrorMessage)
    {
        _lfc = lfc ?? throw new ArgumentNullException(nameof(lfc));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));

        CurrentProfessionalName = currentProfessionalName ?? "";
        TbCollectionName = _collection.classCode ?? "";
        TbRecFolder = recFolder ?? "";
        CanAddCPFs = canAddCpfs;
        CPFsErrorMessage = cpfsErrorMessage ?? "";
    }

    public ObservableCollection<string> BlockTypeOptions { get; } =
        new ObservableCollection<string> { GraduateByCPF.BlockTypes.WATERMARK, GraduateByCPF.BlockTypes.ACCESS_DENIED };

    [ObservableProperty] private string tbCollectionName = "";
    [ObservableProperty] private string currentProfessionalName = "";
    [ObservableProperty] private string tbRecFolder = "";
    [ObservableProperty] private bool canAddCPFs = true;
    [ObservableProperty] private string cPFsErrorMessage = "";

    public ObservableCollection<GraduateByCPFWithPhotos> GraduatesData { get; } = new();

    public Dictionary<string, AddIdItemStatus> ItemStatuses { get; } = new(StringComparer.OrdinalIgnoreCase);

    public AddIdItemStatus GetItemStatus(string shortPath)
    {
        var key = NormalizeShortPath(shortPath);
        if (string.IsNullOrEmpty(key)) return new AddIdItemStatus();
        if (!ItemStatuses.TryGetValue(key, out var status))
        {
            status = new AddIdItemStatus();
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
        SortGraduatesDataAlphabetically();
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

    private void SetItemStatus(string shortPath, AddIdRegistrationStatus status, string failureReason = "")
    {
        var itemStatus = GetItemStatus(shortPath);
        void Apply()
        {
            itemStatus.Status = status;
            itemStatus.FailureReason = failureReason;
        }

        // Na UI thread: aplicar já (evita corrida com IsRegistering=false + RefreshRowStyles antes do Post).
        // Em thread pool: Invoke bloqueia até aplicar na UI, para o estado existir antes do fim do lote.
        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Invoke(Apply);
    }

    private string BuildRegisterResultDialogMessage(int ok, int fail)
    {
        if (fail == 0)
            return "IDs adicionados com sucesso.";

        var header = $"IDs adicionados com sucesso (ok: {ok}, falhas: {fail}).";
        var failedLines = new List<string>();
        foreach (var kvp in ItemStatuses.Where(x => x.Value.Status == AddIdRegistrationStatus.Failed))
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

    [RelayCommand]
    public async Task RegisterGraduatesIds()
    {
        if (_collection == null || string.IsNullOrWhiteSpace(_collection.classCode))
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Selecione uma coleção para continuar.");
            return;
        }

        if (!CanAddCPFs)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(CPFsErrorMessage);
            return;
        }

        var grads = GraduatesData
            .Where(g => g != null && !string.IsNullOrWhiteSpace(g.CPF))
            .Select(g =>
            {
                g.ClassCode = _collection.classCode;
                g.Company = _collection.companyUsername;
                g.Blocked ??= false;
                g.BlockType = string.IsNullOrWhiteSpace(g.BlockType) ? GraduateByCPF.BlockTypes.WATERMARK : g.BlockType;
                g.RegistredBy ??= GraduateByCPF.RegistredByTypes.COMPANY;
                return g;
            })
            .ToList();

        if (grads.Count == 0)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Nenhum ID/CPF para adicionar.");
            return;
        }

        try
        {
            IsRegistering = true;
            RegisterTotalRequests = 0;
            RegisterCompletedRequests = 0;
            RegisterSuccessCount = 0;
            ClearItemStatuses();

            await LoadGraduatesData();
            var existingByCpf = GraduatesData
                .Where(g => g != null && !string.IsNullOrWhiteSpace(g.CPF))
                .GroupBy(g => NormalizeCpf(g.CPF))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToDictionary(g => g.Key, g => g.First());

            var existingShortPaths = new HashSet<string>(
                GraduatesData
                    .Where(g => g != null && !string.IsNullOrWhiteSpace(g.ShortPath))
                    .Select(g => NormalizeShortPath(g.ShortPath)),
                StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(TbRecFolder) || !Directory.Exists(TbRecFolder))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Não foi possível localizar a pasta de reconhecimentos desta coleção neste computador.", "Erro");
                return;
            }

            var items = grads
                .Select(g => new
                {
                    Graduate = g,
                    Cpf = g.CPF?.Trim() ?? "",
                    ShortPathRaw = (g.ShortPath ?? "").Trim()
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Cpf) && !string.IsNullOrWhiteSpace(x.ShortPathRaw))
                .ToList();

            if (items.Count == 0)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Nenhum ID/CPF para adicionar.");
                return;
            }

            RegisterTotalRequests = items.Count;

            const int maxParallel = 4;
            using var sem = new SemaphoreSlim(maxParallel, maxParallel);

            async Task<bool> RegisterOneAsync(GraduateByCPFWithPhotos graduate, string cpf, string shortPathRaw, CancellationToken ct)
            {
                await sem.WaitAsync(ct);
                try
                {
                    var cpfNorm = NormalizeCpf(cpf);
                    var shortPathNorm = NormalizeShortPath(shortPathRaw);
                    var fileNameOnly = Path.GetFileName(shortPathRaw.Replace("/", "\\"));
                    if (string.IsNullOrWhiteSpace(fileNameOnly))
                    {
                        SetItemStatus(shortPathRaw, AddIdRegistrationStatus.Failed, "Nome de arquivo inválido");
                        return false;
                    }

                    var candidate1 = Path.Combine(TbRecFolder, shortPathRaw.TrimStart('\\', '/').Replace("/", "\\"));
                    var candidate2 = Path.Combine(TbRecFolder, fileNameOnly);
                    var fullPath = File.Exists(candidate1) ? candidate1 : (File.Exists(candidate2) ? candidate2 : null);
                    if (fullPath == null)
                    {
                        SetItemStatus(shortPathRaw, AddIdRegistrationStatus.Failed, "Arquivo não encontrado no disco");
                        return false;
                    }

                    byte[] bytes;
                    try
                    {
                        bytes = File.ReadAllBytes(fullPath);
                    }
                    catch (Exception ex)
                    {
                        SetItemStatus(shortPathRaw, AddIdRegistrationStatus.Failed, $"Erro ao ler arquivo: {ex.Message}");
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(cpfNorm) && existingByCpf.ContainsKey(cpfNorm))
                    {
                        if (!existingShortPaths.Contains(shortPathNorm))
                        {
                            var utcr = new UploadPhotoToClassRequest
                            {
                                ClassCode = _collection.classCode,
                                PathInClassFolder = "2.Reconhecimentos_grande/" + shortPathNorm,
                                IsHDPhoto = false,
                            };
                            var uploadResult = await _lfc.UploadPhotoToClass(utcr, bytes, await LesserFunctionClient.GetGCloudAppEndpoint());
                            var uploadOk = uploadResult?.loginFailed != true && uploadResult?.success == true;
                            if (!uploadOk)
                            {
                                SetItemStatus(shortPathRaw, AddIdRegistrationStatus.Failed, "Falha no upload da foto");
                                return false;
                            }

                            existingShortPaths.Add(shortPathNorm);
                        }

                        Interlocked.Increment(ref registerSuccessCount);
                        SetItemStatus(shortPathRaw, AddIdRegistrationStatus.Success);
                        return true;
                    }

                    graduate.CPF = cpfNorm;
                    graduate.ShortPath = fileNameOnly;
                    graduate.ClassCode = _collection.classCode;
                    graduate.Company = _collection.companyUsername;

                    var r = await _lfc.RegisterGraduateByCpf(
                        graduate,
                        bytes,
                        isHDPhoto: false);

                    var success = r?.loginFailed != true && r?.success == true;
                    if (success)
                    {
                        Interlocked.Increment(ref registerSuccessCount);
                        if (!string.IsNullOrWhiteSpace(cpfNorm))
                            existingByCpf[cpfNorm] = new GraduateByCPFWithPhotos { CPF = cpfNorm, ShortPath = shortPathNorm };
                        if (!string.IsNullOrWhiteSpace(shortPathNorm))
                            existingShortPaths.Add(shortPathNorm);
                        SetItemStatus(shortPathRaw, AddIdRegistrationStatus.Success);
                    }
                    else
                    {
                        SetItemStatus(shortPathRaw, AddIdRegistrationStatus.Failed, r?.message ?? "Falha no registro");
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
            var tasks = items.Select(x => RegisterOneAsync(x.Graduate, x.Cpf, x.ShortPathRaw, cts.Token)).ToList();
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

    /// <summary>
    /// Executa o mesmo fluxo visual do registro (progresso, cores por linha, lista de falhas) sem chamar a API.
    /// </summary>
    [RelayCommand]
    public async Task TestAddIdsSimulationAsync()
    {
        const string okPath = "addid_sim_ok.jpg";
        const string failPath = "addid_sim_fail.jpg";

        ClearItemStatuses();

        for (var i = GraduatesData.Count - 1; i >= 0; i--)
        {
            var sp = GraduatesData[i]?.ShortPath;
            if (string.IsNullOrWhiteSpace(sp)) continue;
            var n = NormalizeShortPath(sp);
            if (string.Equals(n, NormalizeShortPath(okPath), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, NormalizeShortPath(failPath), StringComparison.OrdinalIgnoreCase))
                GraduatesData.RemoveAt(i);
        }

        GraduatesData.Add(new GraduateByCPFWithPhotos
        {
            ShortPath = okPath,
            CPF = "11111111111",
            Name = "",
            Blocked = false,
            BlockType = GraduateByCPF.BlockTypes.WATERMARK
        });
        GraduatesData.Add(new GraduateByCPFWithPhotos
        {
            ShortPath = failPath,
            CPF = "22222222222",
            Name = "",
            Blocked = false,
            BlockType = GraduateByCPF.BlockTypes.WATERMARK
        });
        SortGraduatesDataAlphabetically();

        try
        {
            IsRegistering = true;
            RegisterTotalRequests = 2;
            RegisterCompletedRequests = 0;
            RegisterSuccessCount = 0;

            await Task.Delay(400);
            RegisterCompletedRequests = 1;
            RegisterSuccessCount = 1;
            SetItemStatus(okPath, AddIdRegistrationStatus.Success);

            await Task.Delay(400);
            RegisterCompletedRequests = 2;
            SetItemStatus(failPath, AddIdRegistrationStatus.Failed, "Falha no registro");
        }
        finally
        {
            IsRegistering = false;
        }

        var ok = ItemStatuses.Values.Count(v => v.Status == AddIdRegistrationStatus.Success);
        var fail = ItemStatuses.Values.Count(v => v.Status == AddIdRegistrationStatus.Failed);
        GlobalAppStateViewModel.Instance.ShowDialogOk(BuildRegisterResultDialogMessage(ok, fail));
    }

    public bool CanPickNewIdsFromRecFolder() => !string.IsNullOrWhiteSpace(TbRecFolder) && Directory.Exists(TbRecFolder);

    private static string NormalizeCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return "";
        return new string(cpf.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeShortPath(string? shortPath)
    {
        if (string.IsNullOrWhiteSpace(shortPath))
            return "";
        return shortPath.Trim().TrimStart('\\', '/').Replace("\\", "/");
    }
}

