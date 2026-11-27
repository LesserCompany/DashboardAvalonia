using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LesserDashboardClient.Helpers;
using SharedClientSide.Helpers;
using Windows.ApplicationModel;
using Windows.Services.Store;

namespace LesserDashboardClient.Services.Store;

public enum StoreInstallOutcome
{
    /// <summary>Update baixado e instalado; o Windows fecha o app para aplicar.</summary>
    Completed,
    /// <summary>O usuário recusou no diálogo da Store.</summary>
    Canceled,
    /// <summary>A Store não tem update para este app neste momento.</summary>
    NothingToInstall,
    /// <summary>Erro (sem rede, bateria, Wi-Fi exigido, Store indisponível…).</summary>
    Failed
}

/// <summary>
/// Origem da instalação e atualizações pela Microsoft Store, com as classes reais do Windows
/// (TFM net7.0-windows10.0.19041.0). Também é linkado no host MAUI da landing (SecureWebViewApp.csproj):
/// depende só do Windows SDK e do PackagedAppHelper, que os dois projetos têm. Substitui, no Dashboard, o MsixStoreUpdateChecker do SharedClientSide,
/// que procura as classes por reflexão (ContentType=WindowsRuntime) e por isso nunca funcionou em .NET 5+.
/// Docs: StoreContext.GetAppAndOptionalStorePackageUpdatesAsync, StorePackageUpdate.Mandatory,
/// StoreContext.RequestDownloadAndInstallStorePackageUpdatesAsync (desktop: InitializeWithWindow + thread de UI).
/// </summary>
public sealed class StoreUpdateService
{
    public static StoreUpdateService Instance { get; } = new();

    private StoreUpdateService() { }

    private InstallSource? _installSource;

    public InstallSource GetInstallSource() => _installSource ??= ResolveInstallSource();

    private static InstallSource ResolveInstallSource()
    {
        bool packaged = false;
        bool storeSigned = false;
        try
        {
            packaged = PackagedAppHelper.IsRunningAsMsix();
            if (packaged)
                storeSigned = Package.Current.SignatureKind == PackageSignatureKind.Store;
        }
        catch (Exception ex)
        {
            PackagedAppHelper.MsixLog($"StoreUpdateService: ler SignatureKind falhou: {ex.Message}");
        }

        // Mesma pasta de AppInstaller.AppRootFolder; calculada aqui para este arquivo
        // não depender do AppInstaller (ele também é linkado no host MAUI da landing).
        string legacyRoot = string.Empty;
        try
        {
            legacyRoot = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Separacao", "apps");
        }
        catch { /* Documentos indisponível */ }

        InstallSource source = InstallSourceResolver.Resolve(packaged, storeSigned, AppContext.BaseDirectory, legacyRoot);
        PackagedAppHelper.MsixLog($"StoreUpdateService: packaged={packaged}, storeSigned={storeSigned}, source={source}");
        return source;
    }

    /// <summary>
    /// Versão do pacote ("1.0.22.0"), ou a pasta da versão do instalador web ("188").
    /// Vazio quando não há versão confiável (ex.: rodando pelo Visual Studio).
    /// </summary>
    public string GetInstalledVersionLabel()
    {
        try
        {
            switch (GetInstallSource())
            {
                case InstallSource.MicrosoftStore:
                case InstallSource.PackagedOutsideStore:
                    PackageVersion v = Package.Current.Id.Version;
                    return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
                case InstallSource.LegacyWeb:
                    return System.IO.Path.GetFileName(AppContext.BaseDirectory.TrimEnd('\\', '/')) ?? string.Empty;
                default:
                    return string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Pergunta à Store se há update e se algum é obrigatório. Não mostra UI; pode rodar fora da thread de UI.</summary>
    public async Task<StoreUpdateState> CheckStoreUpdatesAsync()
    {
        try
        {
            StoreContext context = StoreContext.GetDefault();
            IReadOnlyList<StorePackageUpdate> updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
            int count = updates?.Count ?? 0;
            bool anyMandatory = updates?.Any(u => u.Mandatory) ?? false;
            StoreUpdateState state = UpdateInstructionsPolicy.StoreStateFrom(count, anyMandatory);
            PackagedAppHelper.MsixLog($"StoreUpdateService: updates={count}, anyMandatory={anyMandatory}, state={state}");
            return state;
        }
        catch (Exception ex)
        {
            PackagedAppHelper.MsixLog($"StoreUpdateService: checagem falhou: {ex.GetType().Name} {ex.Message}");
            return StoreUpdateState.Error;
        }
    }

    /// <summary>
    /// Baixa e instala o update pela Store sem sair do app (a Store mostra o próprio diálogo de confirmação).
    /// Chamar na thread de UI, com o handle da janela dona.
    /// </summary>
    /// <param name="onProgress">0..1 (download até ~0,8; instalação até 1).</param>
    public async Task<StoreInstallOutcome> InstallStoreUpdatesAsync(IntPtr ownerWindowHandle, Action<double>? onProgress = null)
    {
        try
        {
            StoreContext context = StoreContext.GetDefault();
            if (ownerWindowHandle != IntPtr.Zero)
                WinRT.Interop.InitializeWithWindow.Initialize(context, ownerWindowHandle);

            IReadOnlyList<StorePackageUpdate> updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
            if (updates == null || updates.Count == 0)
                return StoreInstallOutcome.NothingToInstall;

            var operation = context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
            if (onProgress != null)
                operation.Progress = (_, status) => onProgress(status.PackageDownloadProgress);

            StorePackageUpdateResult result = await operation;
            PackagedAppHelper.MsixLog($"StoreUpdateService: instalação OverallState={result.OverallState}");

            return result.OverallState switch
            {
                StorePackageUpdateState.Completed => StoreInstallOutcome.Completed,
                StorePackageUpdateState.Canceled => StoreInstallOutcome.Canceled,
                _ => StoreInstallOutcome.Failed
            };
        }
        catch (Exception ex)
        {
            PackagedAppHelper.MsixLog($"StoreUpdateService: instalação falhou: {ex.GetType().Name} {ex.Message}");
            return StoreInstallOutcome.Failed;
        }
    }
}
