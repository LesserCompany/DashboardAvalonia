namespace LesserDashboardClient.Helpers;

/// <summary>
/// Versão de reconhecimento facial enviada na criação/atualização de coleção.
/// A UI não oferece mais 1.0 vs 2.0: o dashboard sempre envia 2.0.
/// </summary>
public static class CollectionRecognitionVersion
{
    public const string Current = "2.0";
}
