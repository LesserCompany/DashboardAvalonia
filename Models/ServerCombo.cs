using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace LesserDashboardClient.Models
{
    /// <summary>
    /// Representa as features/recursos de um combo
    /// </summary>
    public class ServerComboFeatures
    {
        [JsonProperty("uploadHD")]
        public bool UploadHD { get; set; }
        
        [JsonProperty("autoTreatment")]
        public bool AutoTreatment { get; set; }
        
        [JsonProperty("ocr")]
        public bool OCR { get; set; }
        
        [JsonProperty("uploadPhotosAreAlreadySorted")]
        public bool UploadPhotosAreAlreadySorted { get; set; }
        
        [JsonProperty("allowCPFsToSeeAllPhotos")]
        public bool AllowCPFsToSeeAllPhotos { get; set; }
        
        [JsonProperty("enablePhotosSales")]
        public bool EnablePhotosSales { get; set; }
        
        [JsonProperty("enableFaceRelevanceDetection")]
        public bool EnableFaceRelevanceDetection { get; set; }
        
        [JsonProperty("allowDeletedProductionToBeFoundAnyone")]
        public bool AllowDeletedProductionToBeFoundAnyone { get; set; }
        
        [JsonProperty("backupFiveYears")]
        public bool BackupFiveYears { get; set; }
    }

    /// <summary>
    /// Representa um combo do servidor com seu preço e configuração
    /// </summary>
    public class ServerCombo
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        
        [JsonProperty("comboName")]
        public string ComboName { get; set; }
        
        [JsonProperty("price")]
        public double Price { get; set; }
        
        [JsonProperty("coin")]
        public string Coin { get; set; }
        
        [JsonProperty("description")]
        public string Description { get; set; }
        
        [JsonProperty("features")]
        public ServerComboFeatures Features { get; set; }
        
        [JsonProperty("discountPercentage")]
        public double DiscountPercentage { get; set; }
    }
}
