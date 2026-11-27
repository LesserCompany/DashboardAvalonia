namespace LesserDashboardClient.Helpers;

/// <summary>Como este Dashboard foi instalado.</summary>
public enum InstallSource
{
    /// <summary>Fora de pacote e fora da pasta do instalador web (ex.: rodando pelo Visual Studio).</summary>
    Unknown,
    /// <summary>Instalador web antigo (Documentos\Separacao\apps, baixado do Azure).</summary>
    LegacyWeb,
    /// <summary>Pacote MSIX assinado pela Microsoft Store.</summary>
    MicrosoftStore,
    /// <summary>Pacote MSIX instalado fora da Store (letspic.zip).</summary>
    PackagedOutsideStore
}

/// <summary>Descobre a origem da instalação a partir do pacote MSIX e da pasta do executável.</summary>
public static class InstallSourceResolver
{
    /// <param name="isPackaged">Processo tem identidade de pacote (MSIX).</param>
    /// <param name="isStoreSigned">Package.SignatureKind == Store.</param>
    /// <param name="baseDirectory">Pasta do executável (AppContext.BaseDirectory).</param>
    /// <param name="legacyAppsRoot">Documentos\Separacao\apps, onde o instalador web antigo coloca os apps.</param>
    public static InstallSource Resolve(bool isPackaged, bool isStoreSigned, string? baseDirectory, string? legacyAppsRoot)
    {
        if (isPackaged)
            return isStoreSigned ? InstallSource.MicrosoftStore : InstallSource.PackagedOutsideStore;

        string dir = NormalizeDir(baseDirectory);
        string root = NormalizeDir(legacyAppsRoot);
        if (dir.Length == 0 || root.Length == 0)
            return InstallSource.Unknown;

        bool insideLegacyRoot = dir.Equals(root, System.StringComparison.OrdinalIgnoreCase)
                                || dir.StartsWith(root + "\\", System.StringComparison.OrdinalIgnoreCase);
        return insideLegacyRoot ? InstallSource.LegacyWeb : InstallSource.Unknown;
    }

    private static string NormalizeDir(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }
}

/// <summary>O que a Microsoft Store respondeu sobre atualizações deste app.</summary>
public enum StoreUpdateState
{
    None,
    Optional,
    /// <summary>Partner Center marcou o envio como obrigatório: a versão instalada deixou de ser compatível.</summary>
    Mandatory,
    Error
}

public enum UpdateInstructionsMode
{
    None,
    /// <summary>Atualizar pela Microsoft Store (ou desinstalar e instalar de novo por ela).</summary>
    StoreUpdate,
    /// <summary>Desinstalar o app e instalar o letspic.zip.</summary>
    ReinstallFromZip
}

public sealed record UpdateInstructionsDecision(UpdateInstructionsMode Mode, bool IsBlocking)
{
    public bool ShouldShow => Mode != UpdateInstructionsMode.None;
    public bool CanPostpone => ShouldShow && !IsBlocking;

    public static readonly UpdateInstructionsDecision Hidden = new(UpdateInstructionsMode.None, false);
}

/// <summary>
/// Decide se a tela de instruções de atualização abre no startup, em que modo e se bloqueia o app.
/// - Store + update obrigatório: modo Store, bloqueante (fechar encerra o app).
/// - Store + update opcional: modo Store, com "Mais tarde".
/// - Instalador web antigo: sempre modo zip, com "Mais tarde" (migração antes de o Azure ser desligado).
/// - Pacote instalado pelo zip: nada (já está na distribuição nova).
/// - Dashboard aberto pela landing (host MAUI), que já mostrou o aviso opcional: não repete.
/// Usado também pelo host MAUI da landing (arquivo linkado no SecureWebViewApp.csproj).
/// </summary>
public static class UpdateInstructionsPolicy
{
    /// <summary>Argumento que a landing passa ao abrir o Dashboard depois de checar a Store.</summary>
    public const string LandingCheckedArgument = "--store-update-checked";

    public static bool WasCheckedByLanding(string[]? args)
    {
        if (args == null)
            return false;
        foreach (string a in args)
        {
            if (string.Equals(a?.Trim(), LandingCheckedArgument, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static UpdateInstructionsDecision Decide(InstallSource source, StoreUpdateState storeState, bool checkedByLanding = false)
    {
        if (checkedByLanding && source == InstallSource.MicrosoftStore && storeState == StoreUpdateState.Optional)
            return UpdateInstructionsDecision.Hidden;

        switch (source)
        {
            case InstallSource.LegacyWeb:
                return new UpdateInstructionsDecision(UpdateInstructionsMode.ReinstallFromZip, IsBlocking: false);

            case InstallSource.MicrosoftStore:
                return storeState switch
                {
                    StoreUpdateState.Mandatory => new UpdateInstructionsDecision(UpdateInstructionsMode.StoreUpdate, IsBlocking: true),
                    StoreUpdateState.Optional => new UpdateInstructionsDecision(UpdateInstructionsMode.StoreUpdate, IsBlocking: false),
                    _ => UpdateInstructionsDecision.Hidden
                };

            default:
                return UpdateInstructionsDecision.Hidden;
        }
    }

    /// <summary>Variável de ambiente que força a tela no startup, para ver/testar sem update real.</summary>
    public const string DemoEnvironmentVariable = "LETSPIC_UPDATE_SCREEN_DEMO";

    /// <summary>"store-required", "store-optional" ou "zip"; qualquer outro valor = sem demo.</summary>
    public static UpdateInstructionsDecision? TryParseDemo(string? value)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "store-required": return new UpdateInstructionsDecision(UpdateInstructionsMode.StoreUpdate, IsBlocking: true);
            case "store-optional": return new UpdateInstructionsDecision(UpdateInstructionsMode.StoreUpdate, IsBlocking: false);
            case "zip": return new UpdateInstructionsDecision(UpdateInstructionsMode.ReinstallFromZip, IsBlocking: false);
            default: return null;
        }
    }

    /// <summary>
    /// Converte a resposta de StoreContext.GetAppAndOptionalStorePackageUpdatesAsync:
    /// nenhum pacote = sem update; algum com StorePackageUpdate.Mandatory = obrigatório.
    /// </summary>
    public static StoreUpdateState StoreStateFrom(int updateCount, bool anyMandatory)
    {
        if (updateCount <= 0)
            return StoreUpdateState.None;
        return anyMandatory ? StoreUpdateState.Mandatory : StoreUpdateState.Optional;
    }
}
