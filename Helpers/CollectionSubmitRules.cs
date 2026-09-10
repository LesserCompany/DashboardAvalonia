namespace LesserDashboardClient.Helpers;

/// <summary>
/// Distingue "só salvar a task" de "reupload com envio de fotos".
/// </summary>
public static class CollectionSubmitRules
{
    public static bool StartsUploadApp(bool saveOnly) => !saveOnly;

    public static bool SendsPhotoFileLists(bool saveOnly) => !saveOnly;

    public static bool RequiresLocalPhotoFolders(bool saveOnly) => !saveOnly;
}
