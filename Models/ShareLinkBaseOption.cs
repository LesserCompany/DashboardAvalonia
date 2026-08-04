namespace LesserDashboardClient.Models;

/// <summary>
/// Opção de base/domínio para montar o link de compartilhamento da coleção.
/// </summary>
public class ShareLinkBaseOption
{
    /// <summary>Domínio personalizado; null = visualizador.lesser.biz.</summary>
    public string? CustomDomain { get; init; }

    public string DisplayLabel { get; init; } = string.Empty;

    public override string ToString() => DisplayLabel;
}
