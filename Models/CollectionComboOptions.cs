using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LesserDashboardClient.Models
{
    public class CollectionComboOptions : INotifyPropertyChanged
    {
        public bool BackupHd { get; set; }
        public bool AutoTreatment { get; set; }
        public bool Ocr { get; set; }
        public bool EnablePhotoSales { get; set; }
        public bool AllowCPFsToSeeAllPhotos { get; set; }
        public bool AllowDeletedProductionToBeFoundAnyone { get; set; }
        public bool UploadedPhotosAreAlreadySorted { get; set; }

        /// <summary>Espelho do combo no servidor: separação/distribuição por pessoa.</summary>
        public bool PhotosDistribution { get; set; }

        /// <summary>Espelho do combo no servidor: relevância facial / exclusão automática.</summary>
        public bool EnableFaceRelevanceDetection { get; set; }
        
        /// <summary>
        /// Indica se este combo é apenas para tratamento (sem reconhecimento facial)
        /// </summary>
        public bool IsTreatmentOnly { get; set; }


        public string ComboTitle { get; set; }
        public string ComboDescription { get; set; }
        public string ComboColorAccent { get; set; }
        
        /// <summary>
        /// ID do combo no servidor
        /// </summary>
        public int ComboId { get; set; }

        /// <summary>
        /// Ordem de exibição do combo (backend)
        /// </summary>
        public int? ComboOrder { get; set; }

        /// <summary>
        /// Tempo de armazenamento em meses (backend)
        /// </summary>
        public int? StorageTimeMonths { get; set; }

        /// <summary>Indica backup HD de 5 anos (feature do servidor; usado quando <see cref="StorageTimeMonths"/> não veio preenchido).</summary>
        public bool BackupFiveYears { get; set; }

        /// <summary>
        /// Desconto do combo em porcentagem (backend). Pode vir do campo novo comboDiscount ou legado discountPercentage.
        /// </summary>
        public double? DiscountPercentage { get; set; }
        
        /// <summary>
        /// Símbolo da moeda (R$ ou $)
        /// </summary>
        public string CurrencySymbol { get; set; } = "R$";

        /// <summary>
        /// Preço original (antes do desconto) para 1000 fotos, quando fornecido dinamicamente.
        /// </summary>
        public double? ComboOriginalPrice
        {
            get => _dynamicOriginalPrice;
        }

        public double ComboPrice
        {
            get {
                // Se temos um preço dinâmico, usar ele
                if (_dynamicPrice.HasValue)
                {
                    return _dynamicPrice.Value;
                }
                
                // Fallback para cálculo estático (valores antigos)
                double total = 0;
                total += Price_facialRec;
                if (BackupHd) total += Price_backupHd;
                if (AutoTreatment) total += Price_AutoTreatment;
                if (Ocr) total += Price_Ocr;
                return total;
            }
        }

        private double? _dynamicPrice = null;
        private double? _dynamicOriginalPrice = null;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Define o preço dinâmico calculado pelo serviço
        /// </summary>
        public void SetDynamicPrice(double price)
        {
            _dynamicPrice = price;
            // Notificar que o preço mudou
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComboPrice)));
        }

        /// <summary>
        /// Define o preço original dinâmico (antes do desconto), para exibição riscada.
        /// </summary>
        public void SetDynamicOriginalPrice(double? originalPrice)
        {
            _dynamicOriginalPrice = originalPrice;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComboOriginalPrice)));
        }

        /// <summary>
        /// Limpa o preço dinâmico para voltar ao cálculo estático
        /// </summary>
        public void ClearDynamicPrice()
        {
            _dynamicPrice = null;
            _dynamicOriginalPrice = null;
        }

        // Valores estáticos mantidos como fallback
        private double Price_facialRec = 0.03530;
        private double Price_backupHd = 0.0107;
        private double Price_AutoTreatment = 0.0287;
        private double Price_Ocr = 0.0214;
    }
}
