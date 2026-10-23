namespace LesserDashboardClient.Helpers;

/// <summary>
/// Liga ou oculta o tratamento parcial no Dashboard (painel, botão e ícone da lista).
/// </summary>
public static class PartialAutoTreatmentFeature
{
    public const bool Enabled = false;

    public static bool ShowPanel(bool isHdWithoutFullTreatment) =>
        Enabled && isHdWithoutFullTreatment;

    public static bool ShowInList(bool hasBatch) =>
        Enabled && hasBatch;
}
