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

            // Garante que estamos usando o estado mais recente do servidor para mapear
            // - quais CPFs já estão registrados (não chamar criação de novo)
            // - quais shortPaths de IDs já existem (não re-enviar foto)
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

            // Novo comportamento (igual ao Separador NETCORE6): upload + registro via RegisterGraduateByCpf (multipart).
            // Payload é montado dentro do SharedClientSide: token + uploadPhotoToClassRequest + recShortPaths + grads[] + arquivo.
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

            // 1 requisição por ID/CPF. Pode ser em paralelo, mas com limite de concorrência para não sobrecarregar.
            const int maxParallel = 4;
            using var sem = new SemaphoreSlim(maxParallel, maxParallel);

            async Task<bool> RegisterOneAsync(string cpf, string shortPathRaw, CancellationToken ct)
            {
                await sem.WaitAsync(ct);
                try
                {
                    var cpfNorm = NormalizeCpf(cpf);
                    var shortPathNorm = NormalizeShortPath(shortPathRaw);
                    var fileNameOnly = Path.GetFileName(shortPathRaw.Replace("/", "\\"));
                    if (string.IsNullOrWhiteSpace(fileNameOnly))
                        return false;

                    var candidate1 = Path.Combine(TbRecFolder, shortPathRaw.TrimStart('\\', '/').Replace("/", "\\"));
                    var candidate2 = Path.Combine(TbRecFolder, fileNameOnly);
                    var fullPath = File.Exists(candidate1) ? candidate1 : (File.Exists(candidate2) ? candidate2 : null);
                    if (fullPath == null)
                        return false;

                    byte[] bytes;
                    try
                    {
                        bytes = File.ReadAllBytes(fullPath);
                    }
                    catch
                    {
                        return false;
                    }

                    // Se o CPF já está registrado, NÃO chamar criação novamente.
                    // Ainda assim, se a foto (shortPath) ainda não existe no servidor, faz só o upload.
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
                                return false;

                            existingShortPaths.Add(shortPathNorm);
                        }

                        Interlocked.Increment(ref registerSuccessCount);
                        return true;
                    }

                    // CPF ainda não existe: cria + faz upload via RegisterGraduateByCpf (multipart)
                    var r = await _lfc.RegisterGraduateByCpf(
                        _collection.classCode,
                        _collection.companyUsername,
                        cpfNorm,
                        fileNameOnly,
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
            var tasks = items.Select(x => RegisterOneAsync(x.Cpf, x.ShortPathRaw, cts.Token)).ToList();
            var results = await Task.WhenAll(tasks);

            var ok = results.Count(r => r);
            var fail = results.Length - ok;

            GlobalAppStateViewModel.Instance.ShowDialogOk(
                fail == 0
                    ? "IDs adicionados com sucesso."
                    : $"IDs adicionados com sucesso (ok: {ok}, falhas: {fail}).");
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

    public bool CanPickNewIdsFromRecFolder() => !string.IsNullOrWhiteSpace(TbRecFolder) && Directory.Exists(TbRecFolder);

    private static string NormalizeCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return "";
        // Mantém apenas dígitos (evita divergência "123.456.789-00" vs "12345678900")
        return new string(cpf.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeShortPath(string? shortPath)
    {
        if (string.IsNullOrWhiteSpace(shortPath))
            return "";
        return shortPath.Trim().TrimStart('\\', '/').Replace("\\", "/");
    }
}

