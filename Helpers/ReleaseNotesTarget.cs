using System;

namespace LesserDashboardClient.Helpers;

/// <summary>
/// Qual nota de atualização este dashboard pede ao servidor (GetAppReleaseNotes):
/// - instalado em partes (web/AppInstaller): app "LesserDashboard", versão = pasta da instalação ("188");
/// - pacote da Microsoft Store (MSIX): nota geral "MicrosoftStore", versão = versão do pacote ("1.0.42.0"),
///   que resume as notas de todos os apps compilados juntos no pacote.
/// </summary>
public static class ReleaseNotesTarget
{
    public const string DashboardAppName = "LesserDashboard";
    public const string MicrosoftStoreAppName = "MicrosoftStore";

    public static (string AppName, string Version) Resolve(bool isPackaged, string? packageFullName, string? installedFolderVersion)
    {
        if (isPackaged)
            return (MicrosoftStoreAppName, ParsePackageVersion(packageFullName) ?? "");
        return (DashboardAppName, (installedFolderVersion ?? "").Trim());
    }

    /// <summary>"LetsPic.Lesser_1.0.42.0_x64__8wekyb3d8bbwe" -> "1.0.42.0" (nome_versão_arquitetura_recurso_publisher).</summary>
    public static string? ParsePackageVersion(string? packageFullName)
    {
        if (string.IsNullOrWhiteSpace(packageFullName))
            return null;
        var parts = packageFullName.Split('_');
        return parts.Length >= 2 && Version.TryParse(parts[1], out _) ? parts[1] : null;
    }
}
