using Avalonia.Media;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.Helpers;
using OfficeOpenXml;
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

    // Cabeçalho da coluna com o Id do participante no servidor (CompanyCustomersCollectionId) no Excel de
    // "Baixar Excel dos participantes". Ao arrastar o Excel de volta, a linha com Id edita esse participante
    // mesmo que foto, CPF, nome e e-mail tenham mudado; linha sem Id é participante novo.
    public const string ParticipantIdHeader = "Id participante";

    // Ids dos participantes da coleção segundo a última leitura do servidor (carga da tela ou exportação).
    private readonly HashSet<long> _serverParticipantIds = new();

    public bool IsPhotoSalesEnabled => _collection.EnablePhotosSales == true;

    public ObservableCollection<GraduateByCPFWithPhotos> GraduatesData { get; } = new();

    public Dictionary<string, EditIdItemStatus> ItemStatuses { get; } = new(StringComparer.OrdinalIgnoreCase);

    public EditIdItemStatus GetItemStatus(string shortPath)
    {
        var key = GraduateByCpfDuplicateChecker.NormalizeShortPath(shortPath);
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
        if (result?.Content != null)
            RememberServerParticipantIds(graduates);
        foreach (var g in graduates)
            GraduatesData.Add(g);

        foreach (var shortPath in UnregisteredRecognitionShortPaths(GraduatesData))
        {
            GraduatesData.Add(new GraduateByCPFWithPhotos
            {
                ShortPath = shortPath,
                Name = "",
                Blocked = false,
                BlockType = GraduateByCPF.BlockTypes.WATERMARK
            });
        }
        SortGraduatesDataAlphabetically();
    }

    private void RememberServerParticipantIds(IEnumerable<GraduateByCPF> serverGraduates)
    {
        _serverParticipantIds.Clear();
        foreach (var g in serverGraduates)
            if (g?.CompanyCustomersCollectionId is long id && id > 0)
                _serverParticipantIds.Add(id);
    }

    /// <summary>
    /// Shortpaths de reconhecimento da pasta local da PT que ainda não estão em <paramref name="known"/>
    /// (mesmo comportamento do New ID no Add IDs): entram na grade e no Excel como participantes novos.
    /// </summary>
    private List<string> UnregisteredRecognitionShortPaths(IEnumerable<GraduateByCPF> known)
    {
        var found = new List<string>();
        if (string.IsNullOrWhiteSpace(_recFolder) || !Directory.Exists(_recFolder))
            return found;

        if (!FileHelper.TryGetFilesWithExtensionsAndFilters(_recFolder, out var gradPhotos, out _, out _))
            return found;

        var recRoot = _recFolder.TrimEnd('\\', '/');
        var existingShortPaths = new HashSet<string>(
            known
                .Where(g => g != null && !string.IsNullOrWhiteSpace(g.ShortPath))
                .Select(g => GraduateByCpfDuplicateChecker.NormalizeShortPath(g.ShortPath)),
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

            var normalized = GraduateByCpfDuplicateChecker.NormalizeShortPath(shortPath);
            if (existingShortPaths.Contains(normalized))
                continue;

            found.Add(shortPath);
            existingShortPaths.Add(normalized);
        }
        return found;
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
            var cpf = GraduatesData.FirstOrDefault(g => string.Equals(GraduateByCpfDuplicateChecker.NormalizeShortPath(g?.ShortPath), key, StringComparison.OrdinalIgnoreCase))?.CPF?.Trim();
            var cpfPart = string.IsNullOrWhiteSpace(cpf) ? "" : $" (CPF {cpf})";
            var reason = kvp.Value.FailureReason?.Trim();
            var detail = string.IsNullOrWhiteSpace(reason) ? "" : $" — {reason}";
            failedLines.Add($"• {key}{cpfPart}{detail}");
        }

        if (failedLines.Count == 0)
            return header;

        return header + "\n\n" + string.Join("\n", failedLines);
    }

    private static GraduateByCPF PrepareGraduateForUpload(GraduateByCPFWithPhotos g, string classCode, string company, bool photoSalesEnabled)
    {
        g.ClassCode = classCode;
        g.Company = company;
        g.Blocked ??= false;
        g.BlockType = string.IsNullOrWhiteSpace(g.BlockType) ? GraduateByCPF.BlockTypes.WATERMARK : g.BlockType;
        g.RegistredBy ??= GraduateByCPF.RegistredByTypes.COMPANY;
        if (!photoSalesEnabled)
            g.GraduateCanViewAllPhotosFromThisClassCode = null;
        else
            g.GraduateCanViewAllPhotosFromThisClassCode = g.GraduateCanViewAllPhotosFromThisClassCode ?? false;
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

        var duplicateMessage = GraduateByCpfDuplicateChecker.ValidateOrGetMessage(GraduatesData);
        if (duplicateMessage != null)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(duplicateMessage, "Formandos duplicados");
            return;
        }

        var grads = GraduatesData
            .Where(g => g != null && !string.IsNullOrWhiteSpace(g.CPF))
            .Select(g => PrepareGraduateForUpload(g, _collection.classCode, _collection.companyUsername, IsPhotoSalesEnabled))
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
        await collectionsVm.GenerateAndOpenExcelForGraduatesAsync(working, _recFolder, CanEditCPFs, CPFsErrorMessage, IsPhotoSalesEnabled);
        ApplyImportedGraduates(working);
    }

    public string? ImportGraduateDataFromExcelFile(FileInfo f)
    {
        var collectionsVm = CollectionsViewModel.Instance;
        if (collectionsVm == null)
            return "Erro interno.";

        var working = new ObservableCollection<GraduateByCPF>(GraduatesData.Cast<GraduateByCPF>());
        var err = collectionsVm.ImportGraduatesFromExcel(f, working, _recFolder, CanEditCPFs, CPFsErrorMessage, IsPhotoSalesEnabled, readParticipantId: true);
        if (err != null)
            return err;

        err = ValidateParticipantIds(working);
        if (err != null)
            return err;

        ApplyImportedGraduates(working);
        return null;
    }

    // Id vindo do Excel tem que ser de um participante desta coleção e aparecer uma vez só; senão a linha
    // editaria outro participante (ou dois participantes virariam um).
    private string? ValidateParticipantIds(IEnumerable<GraduateByCPF> imported)
    {
        var seen = new Dictionary<long, string>();
        foreach (var g in imported)
        {
            if (g?.CompanyCustomersCollectionId is not long id)
                continue;

            var photo = string.IsNullOrWhiteSpace(g.ShortPath) ? "(sem foto)" : g.ShortPath;
            if (!_serverParticipantIds.Contains(id))
                return $"O {ParticipantIdHeader} {id} (foto {photo}) não pertence a esta coleção.\n\n" +
                       "Use o Excel gerado por \"Baixar Excel dos participantes\" desta coleção, sem alterar a coluna " +
                       $"\"{ParticipantIdHeader}\". Nas linhas de participantes novos, deixe essa coluna vazia.";

            if (seen.TryGetValue(id, out var otherPhoto))
                return $"O {ParticipantIdHeader} {id} aparece em mais de uma linha (fotos {otherPhoto} e {photo}).\n\n" +
                       "Cada participante deve aparecer uma única vez. Corrija o Excel e tente novamente.";
            seen[id] = photo;
        }
        return null;
    }

    private void ApplyImportedGraduates(ObservableCollection<GraduateByCPF> source)
    {
        var idsByShortPath = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in GraduatesData)
        {
            if (existing?.CompanyCustomersCollectionId is not long id || id <= 0)
                continue;
            var key = GraduateByCpfDuplicateChecker.NormalizeShortPath(existing.ShortPath);
            if (!string.IsNullOrEmpty(key))
                idsByShortPath[key] = id;
        }

        // Id que veio do Excel vence; o da foto (ShortPath) só completa linha sem Id e se nenhuma outra linha já
        // usa esse Id (ex.: participante cuja foto foi trocada e outra linha nova com a foto antiga).
        var usedIds = new HashSet<long>(source
            .Select(g => g?.CompanyCustomersCollectionId)
            .Where(id => id is long v && v > 0)
            .Select(id => id!.Value));

        GraduatesData.Clear();
        foreach (var g in source)
        {
            // O construtor de cópia só copia IGraduateByCPF, que não tem o Id.
            var row = g is GraduateByCPFWithPhotos wp ? wp : new GraduateByCPFWithPhotos(g) { CompanyCustomersCollectionId = g.CompanyCustomersCollectionId };
            var key = GraduateByCpfDuplicateChecker.NormalizeShortPath(row.ShortPath);
            if ((row.CompanyCustomersCollectionId is not long current || current <= 0)
                && !string.IsNullOrEmpty(key)
                && idsByShortPath.TryGetValue(key, out var preserved)
                && usedIds.Add(preserved))
                row.CompanyCustomersCollectionId = preserved;
            GraduatesData.Add(row);
        }
        SortGraduatesDataAlphabetically();
    }

    /// <summary>
    /// "Baixar Excel dos participantes": grava em <paramref name="output"/> todos os participantes da coleção,
    /// lidos de novo do servidor (nunca da grade, que pode ter edições não enviadas), com a coluna
    /// <see cref="ParticipantIdHeader"/> = Id dado pelo servidor. Fotos da pasta de reconhecimento ainda sem
    /// participante entram no fim, com o Id vazio (participantes novos). Mesmas colunas de "Editar dados no excel",
    /// então o arquivo volta pela área de arrastar. Retorna null ou a mensagem de erro.
    /// </summary>
    public async Task<string?> ExportParticipantsExcelAsync(Stream output)
    {
        var result = await _lfc.GetGraduatesByCPFByClassCode(_collection.classCode);
        if (result?.Content == null || result.success != true)
            return "Não foi possível buscar os participantes da coleção no servidor. Tente novamente.";

        var participants = result.Content.Where(g => g != null).Cast<GraduateByCPF>().ToList();
        RememberServerParticipantIds(participants);

        var withoutId = participants.Count(g => g.CompanyCustomersCollectionId is not long id || id <= 0);
        if (withoutId > 0)
            return $"O servidor não informou o {ParticipantIdHeader} de {withoutId} participante(s). O Excel não foi gerado. Tente novamente mais tarde.";

        // Mesma ordem da grade (pela foto), fotos novas no meio. Participante sem foto vai pro fim: a importação
        // para na primeira linha com a foto vazia.
        var rows = participants
            .Select(g => (Id: g.CompanyCustomersCollectionId, ShortPath: g.ShortPath, Graduate: (GraduateByCPF?)g))
            .Concat(UnregisteredRecognitionShortPaths(participants).Select(p => (Id: (long?)null, ShortPath: p, Graduate: (GraduateByCPF?)null)))
            .OrderBy(x => string.IsNullOrWhiteSpace(x.ShortPath) ? 1 : 0)
            .ThenBy(x => x.ShortPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var excel = new ExcelPackage();
        var ws = excel.Workbook.Worksheets.Add("Participantes");

        var headers = new[]
        {
            ParticipantIdHeader,
            TranslationHelper.Default.PHOTO_NAME,
            TranslationHelper.Default.ID,
            Loc.Tr("Name"),
            "Email",
            TranslationHelper.Default.MAX_PHOTOS,
            TranslationHelper.Default.MAX_PHOTOS_FOR_TREATMENT,
            TranslationHelper.Default.FREE_PHOTOS_FROM_THIS_CLASS_CODE,
            TranslationHelper.Default.GRADUATE_CAN_VIEW_ALL_PHOTOS_FROM_THIS_CLASS_CODE,
            TranslationHelper.Default.BLOCKED,
            TranslationHelper.Default.BLOCK_MODE
        };
        for (var c = 0; c < headers.Length; c++)
            ws.Cells[1, c + 1].Value = headers[c];

        void WriteRow(int r, long? id, string? shortPath, GraduateByCPF? g)
        {
            if (id != null)
                ws.Cells[r, 1].Value = id.Value;
            ws.Cells[r, 2].Value = string.IsNullOrWhiteSpace(shortPath) ? null : shortPath;   // vazia = fim da importação
            ws.Cells[r, 3].Value = g?.CPF ?? "";
            ws.Cells[r, 4].Value = g?.Name ?? "";
            ws.Cells[r, 5].Value = g?.Email ?? "";
            ws.Cells[r, 6].Value = g?.MaxPhotos?.ToString() ?? "";
            ws.Cells[r, 7].Value = g?.MaxPhotosForTreatmentRequest?.ToString() ?? "";
            ws.Cells[r, 8].Value = g?.FreePhotosFromThisClassCode?.ToString() ?? "";
            ws.Cells[r, 9].Value = g?.GraduateCanViewAllPhotosFromThisClassCode?.ToString() ?? "";
            ws.Cells[r, 10].Value = (g?.Blocked ?? false).ToString();
            ws.Cells[r, 11].Value = string.IsNullOrWhiteSpace(g?.BlockType) ? GraduateByCPF.BlockTypes.WATERMARK : g!.BlockType;
        }

        var row = 2;
        foreach (var x in rows)
            WriteRow(row++, x.Id, x.ShortPath, x.Graduate);

        var lastRow = Math.Max(row - 1, 1);
        ws.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
        ws.Cells[1, 1, lastRow, 1].Style.Numberformat.Format = "0";
        ws.Cells[1, 1, lastRow, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        ws.Cells[1, 1, lastRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(230, 230, 230));
        ws.Cells[1, 1].AddComment("Preenchido pelo sistema: não altere. Linha sem Id = participante novo.", "Lesser");
        ws.Cells[1, 3, lastRow, 3].Style.Numberformat.Format = "@";   // CPF como texto (mantém zeros à esquerda)
        ws.View.FreezePanes(2, 1);
        ws.Column(1).Width = 16;
        ws.Column(2).Width = 60;
        for (var c = 3; c <= headers.Length; c++)
            ws.Column(c).Width = c <= 5 ? 30 : 18;

        await excel.SaveAsAsync(output);
        return null;
    }
}
