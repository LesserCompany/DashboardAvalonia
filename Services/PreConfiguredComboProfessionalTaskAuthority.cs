using System;
using System.Collections.Generic;
using System.Reflection;
using LesserDashboardClient.Models;
using SharedClientSide.ServerInteraction;

namespace LesserDashboardClient.Services
{
    /// <summary>
    /// Coleção pré-configurada: copia da <see cref="CollectionComboOptions"/> para a <see cref="ProfessionalTask"/>
    /// o que existir na PT, espelhando o combo (autoridade máxima).
    /// Novo campo do combo: adicione um par (propriedade no combo → propriedade na PT) em <see cref="ComboToProfessionalTaskPropertyMap"/>.
    /// </summary>
    public static class PreConfiguredComboProfessionalTaskAuthority
    {
        /// <summary>
        /// Chave = nome da propriedade em <see cref="CollectionComboOptions"/>; valor = nome em <see cref="ProfessionalTask"/>.
        /// Inclui renomes necessários (ex. BackupHd → UploadHD, EnablePhotoSales → EnablePhotosSales).
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> ComboToProfessionalTaskPropertyMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [nameof(CollectionComboOptions.PhotosDistribution)] = nameof(ProfessionalTask.PhotosDistribution),
                [nameof(CollectionComboOptions.EnableFaceRelevanceDetection)] = nameof(ProfessionalTask.EnableFaceRelevanceDetection),
                [nameof(CollectionComboOptions.BackupHd)] = nameof(ProfessionalTask.UploadHD),
                [nameof(CollectionComboOptions.AutoTreatment)] = nameof(ProfessionalTask.AutoTreatment),
                [nameof(CollectionComboOptions.Ocr)] = nameof(ProfessionalTask.OCR),
                [nameof(CollectionComboOptions.EnablePhotoSales)] = nameof(ProfessionalTask.EnablePhotosSales),
                [nameof(CollectionComboOptions.AllowCPFsToSeeAllPhotos)] = nameof(ProfessionalTask.AllowCPFsToSeeAllPhotos),
                [nameof(CollectionComboOptions.AllowDeletedProductionToBeFoundAnyone)] = nameof(ProfessionalTask.AllowDeletedProductionToBeFoundAnyone),
                [nameof(CollectionComboOptions.UploadedPhotosAreAlreadySorted)] = nameof(ProfessionalTask.UploadPhotosAreAlreadySorted),
                [nameof(CollectionComboOptions.BackupFiveYears)] = nameof(ProfessionalTask.BackupFiveYears),
                [nameof(CollectionComboOptions.IsTreatmentOnly)] = nameof(ProfessionalTask.IsTreatmentOnly),
            };

        public static void Apply(CollectionComboOptions combo, ProfessionalTask pt)
        {
            if (combo == null || pt == null) return;

            var comboType = typeof(CollectionComboOptions);
            var ptType = typeof(ProfessionalTask);
            var flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var kv in ComboToProfessionalTaskPropertyMap)
            {
                var cProp = comboType.GetProperty(kv.Key, flags);
                var pProp = ptType.GetProperty(kv.Value, flags);
                if (cProp == null || pProp == null || !pProp.CanWrite) continue;

                var raw = cProp.GetValue(combo);

                if (cProp.PropertyType == typeof(bool) && pProp.PropertyType == typeof(bool?))
                    pProp.SetValue(pt, (bool?)((bool)raw));
                else if (cProp.PropertyType == pProp.PropertyType)
                    pProp.SetValue(pt, raw);
            }

            if (combo.AutoTreatment)
                pt.AutoTreatmentVersion = "2.0";
        }
    }
}
