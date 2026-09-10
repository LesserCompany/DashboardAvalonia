namespace LesserDashboardClient.Helpers;

/// <summary>
/// Regras do botão "Solicitar Reprocessamento": só faz sentido quando
/// reconhecimento/OCR/tratamento ainda não chegaram a 100%.
/// </summary>
public static class CollectionReprocessRules
{
    public static bool IsFullyComplete(
        int? total,
        int done,
        int ocrDone,
        int autoTreatedDone,
        bool ocrEnabled,
        bool autoTreatmentEnabled)
    {
        int totalPhotos = total ?? 0;
        if (totalPhotos == 0)
            return true;

        bool recognitionComplete = done >= totalPhotos;
        bool ocrComplete = !ocrEnabled || ocrDone >= totalPhotos;
        bool autoTreatmentComplete = !autoTreatmentEnabled || autoTreatedDone >= totalPhotos;
        return recognitionComplete && ocrComplete && autoTreatmentComplete;
    }

    /// <summary>
    /// Visível apenas com dados de progresso e coleção ainda incompleta.
    /// Sem progresso carregado, o botão permanece oculto.
    /// </summary>
    public static bool ShouldShowReprocessButton(bool hasProgressData, bool isFullyComplete) =>
        hasProgressData && !isFullyComplete;
}
