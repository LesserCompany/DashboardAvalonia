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


        public string ComboTitle { get; set; }
        public string ComboDescription { get; set; }
        public double ComboPrice { get; set; }
        public string ComboColorAccent { get; set; }
    }
}
