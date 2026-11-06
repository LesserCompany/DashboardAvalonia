using System.Collections.Generic;
using LesserDashboardClient.Models;
using SharedClientSide.ServerInteraction;

namespace LesserDashboardClient.Services
{
    /// <summary>
    /// Descobre o combo de uma coleção. A coleção não guarda qual combo foi escolhido: o fechamento da fatura
    /// (PgService.IsCombo / ComboFeaturesMatch no servidor) considera combo quando as 9 opções da coleção são
    /// iguais às do combo, e usa o primeiro que bater. Aqui segue a mesma regra, para o dashboard mostrar o combo
    /// que será cobrado. Opção nula na coleção não conta como false (no servidor, Equals(null, false) é false).
    /// </summary>
    public static class CollectionComboMatcher
    {
        public static CollectionComboOptions? FindCombo(ProfessionalTask? pt, IEnumerable<CollectionComboOptions>? combos)
        {
            if (pt == null || combos == null)
                return null;

            foreach (var combo in combos)
                if (combo != null && Matches(pt, combo))
                    return combo;

            return null;
        }

        private static bool Matches(ProfessionalTask pt, CollectionComboOptions c) =>
            pt.UploadHD == c.BackupHd
            && pt.AutoTreatment == c.AutoTreatment
            && pt.OCR == c.Ocr
            && pt.UploadPhotosAreAlreadySorted == c.UploadedPhotosAreAlreadySorted
            && pt.AllowCPFsToSeeAllPhotos == c.AllowCPFsToSeeAllPhotos
            && pt.EnablePhotosSales == c.EnablePhotoSales
            && pt.AllowDeletedProductionToBeFoundAnyone == c.AllowDeletedProductionToBeFoundAnyone
            && pt.PhotosDistribution == c.PhotosDistribution
            && pt.EnableFaceRelevanceDetection == c.EnableFaceRelevanceDetection;
    }
}
