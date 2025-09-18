using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LesserDashboardClient.Models
{
    public class CollectionComboOptions
    {
        public bool BackupHd { get; set; }
        public bool AutoTreatment { get; set; }
        public bool Ocr { get; set; }
        public bool EnablePhotoSales { get; set; }
        public bool AllowCPFsToSeeAllPhotos { get; set; }
        public bool AllowDeletedProductionToBeFoundAnyone { get; set; }
        public bool UploadedPhotosAreAlreadySorted { get; set; }


        public string ComboTitle { get; set; }
        public string ComboDescription { get; set; }
        public string ComboColorAccent { get; set; }
        public double ComboPrice
        {
            get {
                double total = 0;
                total += Price_facialRec;
                if (BackupHd) total += Price_backupHd;
                if (AutoTreatment) total += Price_AutoTreatment;
                if (Ocr) total += Price_Ocr;
                return total;
            }
        }


        private double Price_facialRec = 0.03530;
        private double Price_backupHd = 0.0107;
        private double Price_AutoTreatment = 0.0287;
        private double Price_Ocr = 0.0214;
    }
}
