using System;
using System.Threading.Tasks;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Companies;
using LesserDashboardClient.Models;

namespace LesserDashboardClient.Services
{

    public class PricesData
    {
        public double? PhotoPrice { get; set; }
        public double? PhotosDistributionPricePerPhoto { get; set; }
        public double? UploadHDPricePerPhoto { get; set; }
        public double? DiscountPerPhoto { get; set; }
        public double? FaceRelevanceDetectionPricePerPhoto { get; set; }
        public double? AutoTreatmentPricePerPhoto { get; set; }
        public double? OcrPricePerPhoto { get; set; }
    }

    public class ComboPriceService
    {
        private static PricesData _pricesData;
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Obtém os dados de preços da empresa com cache
        /// </summary>
        public static async Task<PricesData> GetCompanyPricesAsync()
        {
            var now = DateTime.Now;
            
            // Verificar se temos dados em cache válidos
            if (_pricesData != null && (now - _lastFetchTime) < CacheDuration)
            {
                return _pricesData;
            }

            try
            {
                Console.WriteLine("ComboPriceService: Iniciando busca de dados da empresa...");
                
                if (ViewModels.GlobalAppStateViewModel.lfc == null)
                {
                    Console.WriteLine("ComboPriceService: LesserFunctionClient não está inicializado");
                    throw new Exception("LesserFunctionClient não está inicializado");
                }

                Console.WriteLine("ComboPriceService: Chamando API GetCompanyDetails...");
                var result = await ViewModels.GlobalAppStateViewModel.lfc.GetCompanyDetails();
                
                Console.WriteLine($"ComboPriceService: Resultado da API - success: {result.success}");
                
                if (!result.success || result.Content == null)
                {
                    Console.WriteLine($"ComboPriceService: Falha na API - success: {result.success}, Content: {result.Content}");
                    throw new Exception("Falha ao obter dados da empresa");
                }

                // Acessar diretamente as propriedades da classe Company
                _pricesData = new PricesData
                {
                    PhotoPrice = result.Content.photoPrice,
                    PhotosDistributionPricePerPhoto = result.Content.PhotosDistributionPricePerPhoto,
                    UploadHDPricePerPhoto = result.Content.UploadHDPricePerPhoto,
                    DiscountPerPhoto = result.Content.DiscountPerPhoto,
                    FaceRelevanceDetectionPricePerPhoto = result.Content.FaceRelevanceDetectionPricePerPhoto,
                    AutoTreatmentPricePerPhoto = result.Content.AutoTreatmentPricePerPhoto,
                    OcrPricePerPhoto = result.Content.OcrPricePerPhoto
                };
                
                _lastFetchTime = now;
                
                Console.WriteLine($"ComboPriceService: Dados de preços obtidos com sucesso:");
                Console.WriteLine($"  - photoPrice: {_pricesData.PhotoPrice}");
                Console.WriteLine($"  - faceRelevanceDetectionPricePerPhoto: {_pricesData.FaceRelevanceDetectionPricePerPhoto}");
                Console.WriteLine($"  - uploadHDPricePerPhoto: {_pricesData.UploadHDPricePerPhoto}");
                Console.WriteLine($"  - autoTreatmentPricePerPhoto: {_pricesData.AutoTreatmentPricePerPhoto}");
                Console.WriteLine($"  - photosDistributionPricePerPhoto: {_pricesData.PhotosDistributionPricePerPhoto}");
                Console.WriteLine($"  - ocrPricePerPhoto: {_pricesData.OcrPricePerPhoto}");
                
                return _pricesData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter dados da empresa: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Calcula o preço de um combo baseado na configuração e preços da empresa
        /// Os preços vêm em centavos do endpoint, então convertemos para reais e multiplicamos por 1000
        /// </summary>
        public static async Task<double> CalculateComboPriceAsync(CollectionComboOptions config)
        {
            try
            {
                var prices = await GetCompanyPricesAsync();
                
                double totalPriceInCents = 0;

                // photoPrice - sempre aplicado em todos os combos
                totalPriceInCents += prices.PhotoPrice ?? 0;

                // faceRelevanceDetectionPricePerPhoto - sempre aplicado em todos os combos
                totalPriceInCents += prices.FaceRelevanceDetectionPricePerPhoto ?? 0;

                // uploadHDPricePerPhoto - quando Backup HD estiver habilitado
                if (config.BackupHd)
                {
                    totalPriceInCents += prices.UploadHDPricePerPhoto ?? 0;
                }

                // autoTreatmentPricePerPhoto - quando Tratamento automático com IA estiver habilitado
                if (config.AutoTreatment)
                {
                    totalPriceInCents += prices.AutoTreatmentPricePerPhoto ?? 0;
                }

                // ocrPricePerPhoto - quando Reconhecimento de texto estiver habilitado
                if (config.Ocr)
                {
                    totalPriceInCents += prices.OcrPricePerPhoto ?? 1.15; // Usar 1.15 centavos quando null
                }

                // photosDistributionPricePerPhoto - quando CPFs podem ver todas as fotos estiver habilitado
                if (config.AllowCPFsToSeeAllPhotos)
                {
                    totalPriceInCents += prices.PhotosDistributionPricePerPhoto ?? 0;
                }

                // Desconto comentado por enquanto
                // if (prices.DiscountPerPhoto > 0)
                // {
                //     totalPriceInCents -= prices.DiscountPerPhoto ?? 0;
                // }

                // Converter centavos para reais e multiplicar por 1000 fotos
                double totalPriceInReais = (totalPriceInCents / 100.0) * 1000.0;

                Console.WriteLine($"ComboPriceService: Calculando preço para '{config.ComboTitle}': {totalPriceInCents:F4} centavos = R$ {totalPriceInReais:F2} para 1000 fotos");
                Console.WriteLine($"  - photoPrice: {prices.PhotoPrice ?? 0:F4} centavos");
                Console.WriteLine($"  - faceRelevanceDetection: {prices.FaceRelevanceDetectionPricePerPhoto ?? 0:F4} centavos");
                Console.WriteLine($"  - backupHd: {config.BackupHd} = {(config.BackupHd ? prices.UploadHDPricePerPhoto ?? 0 : 0):F4} centavos");
                Console.WriteLine($"  - autoTreatment: {config.AutoTreatment} = {(config.AutoTreatment ? prices.AutoTreatmentPricePerPhoto ?? 0 : 0):F4} centavos");
                Console.WriteLine($"  - ocr: {config.Ocr} = {(config.Ocr ? prices.OcrPricePerPhoto ?? 1.15 : 0):F4} centavos");
                Console.WriteLine($"  - allowCPFs: {config.AllowCPFsToSeeAllPhotos} = {(config.AllowCPFsToSeeAllPhotos ? prices.PhotosDistributionPricePerPhoto ?? 0 : 0):F4} centavos");

                // Garantir que o preço não seja negativo
                return Math.Max(0, totalPriceInReais);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao calcular preço do combo '{config.ComboTitle}': {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return 0;
            }
        }

        /// <summary>
        /// Atualiza os preços de todos os combos com valores dinâmicos
        /// </summary>
        public static async Task UpdateCombosWithDynamicPricesAsync(CollectionComboOptions[] combos)
        {
            Console.WriteLine($"ComboPriceService: Iniciando atualização de preços para {combos.Length} combos");
            
            foreach (var combo in combos)
            {
                try
                {
                    Console.WriteLine($"ComboPriceService: Processando combo '{combo.ComboTitle}'");
                    var dynamicPrice = await CalculateComboPriceAsync(combo);
                    combo.SetDynamicPrice(dynamicPrice);
                    Console.WriteLine($"ComboPriceService: Preço definido para '{combo.ComboTitle}': R$ {dynamicPrice:F4}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao calcular preço para combo '{combo.ComboTitle}': {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    // Manter o preço original em caso de erro
                }
            }
            
            Console.WriteLine("ComboPriceService: Atualização de preços concluída");
        }

        /// <summary>
        /// Obtém os dados da empresa com preços
        /// </summary>
        public static async Task<PricesData> GetCurrentPricesAsync()
        {
            return await GetCompanyPricesAsync();
        }

        /// <summary>
        /// Limpa o cache de dados da empresa
        /// </summary>
        public static void ClearCache()
        {
            _pricesData = null;
            _lastFetchTime = DateTime.MinValue;
            Console.WriteLine("ComboPriceService: Cache limpo");
        }
    }
}
