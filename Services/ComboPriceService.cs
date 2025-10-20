using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Companies;
using LesserDashboardClient.Models;
using LesserDashboardClient.ViewModels;

namespace LesserDashboardClient.Services
{
    public class ComboPriceService
    {
        private static List<ServerCombo> _cachedCombos;
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Obtém os combos do servidor com cache
        /// </summary>
        public static async Task<List<ServerCombo>> GetCompanyComboPricesAsync()
        {
            var now = DateTime.Now;
            
            // Verificar se temos dados em cache válidos
            if (_cachedCombos != null && (now - _lastFetchTime) < CacheDuration)
            {
                return _cachedCombos;
            }

            try
            {
                if (ViewModels.GlobalAppStateViewModel.lfc == null)
                {
                    throw new Exception("LesserFunctionClient não está inicializado");
                }
                
                // Obter o idioma atual das configurações
                string currentLanguage = GetCurrentLanguageCode();
                
                // Usar o LesserFunctionClient seguindo o padrão do projeto
                var result = await ViewModels.GlobalAppStateViewModel.lfc.GetCompanyComboPrices(currentLanguage);
                
                if (!result.success)
                {
                    throw new Exception($"Erro na API: {result.message}");
                }
                
                // Converter List<object> para List<ServerCombo>
                var serverCombos = new List<ServerCombo>();
                if (result.Content != null)
                {
                    foreach (var item in result.Content)
                    {
                        try
                        {
                            var json = Newtonsoft.Json.JsonConvert.SerializeObject(item);
                            var serverCombo = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerCombo>(json);
                            if (serverCombo != null)
                            {
                                serverCombos.Add(serverCombo);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log error silently
                        }
                    }
                }

                _cachedCombos = serverCombos;
                _lastFetchTime = now;
                
                return _cachedCombos;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Converte um combo do servidor para CollectionComboOptions do client-side
        /// </summary>
        private static CollectionComboOptions ConvertServerComboToClientCombo(ServerCombo serverCombo)
        {
            return new CollectionComboOptions
            {
                ComboTitle = serverCombo.ComboName,
                ComboDescription = serverCombo.Description,
                ComboColorAccent = GetColorForCombo(serverCombo.ComboName),
                BackupHd = serverCombo.Features.UploadHD,
                AutoTreatment = serverCombo.Features.AutoTreatment,
                EnablePhotoSales = serverCombo.Features.EnablePhotosSales,
                AllowCPFsToSeeAllPhotos = serverCombo.Features.AllowCPFsToSeeAllPhotos,
                AllowDeletedProductionToBeFoundAnyone = serverCombo.Features.AllowDeletedProductionToBeFoundAnyone,
                Ocr = serverCombo.Features.OCR,
                UploadedPhotosAreAlreadySorted = serverCombo.Features.UploadPhotosAreAlreadySorted,
                IsTreatmentOnly = IsTreatmentOnlyCombo(serverCombo.ComboName),
                ComboId = serverCombo.Id,
                CurrencySymbol = GetCurrencySymbol(serverCombo.Coin)
            };
        }

        /// <summary>
        /// Define uma cor baseada no nome do combo
        /// </summary>
        private static string GetColorForCombo(string comboName)
        {
            var name = comboName.ToLower();
            
            if (name.Contains("reconhecimento facial") && !name.Contains("visualização"))
                return "#B0B0B0"; // cinza neutro
            else if (name.Contains("visualização online"))
                return "#6495ED"; // azul claro
            else if (name.Contains("tratamento") && name.Contains("ia"))
                return "#FF8C00"; // laranja
            else if (name.Contains("venda"))
                return "#32CD32"; // verde
            else if (name.Contains("completo") || name.Contains("ocr"))
                return "#8A2BE2"; // roxo
            else if (name.Contains("apenas tratamento"))
                return "Orange";
            else if (name.Contains("gratuitamente"))
                return "Pink";
            else
                return "#6495ED"; // azul padrão
        }

        /// <summary>
        /// Verifica se o combo é apenas para tratamento (sem reconhecimento facial)
        /// </summary>
        private static bool IsTreatmentOnlyCombo(string comboName)
        {
            var name = comboName.ToLower();
            
            // Combos que são apenas tratamento - deve conter especificamente "apenas tratamento"
            return name.Contains("apenas tratamento");
        }

        /// <summary>
        /// Obtém o símbolo da moeda baseado no tipo de moeda
        /// </summary>
        private static string GetCurrencySymbol(string coin)
        {
            if (string.IsNullOrEmpty(coin))
                return "R$"; // Fallback para real

            switch (coin.ToLower())
            {
                case "real":
                    return "R$";
                case "dolar":
                case "dollar":
                case "usd":
                    return "$";
                default:
                    return "R$"; // Fallback para real
            }
        }

        /// <summary>
        /// Cria e retorna todos os combos dinamicamente baseados no servidor
        /// </summary>
        public static async Task<List<CollectionComboOptions>> GetDynamicCombosAsync()
        {
            try
            {
                // Buscar todos os combos do servidor
                var serverCombos = await GetCompanyComboPricesAsync();
                
                if (serverCombos == null || serverCombos.Count == 0)
                {
                    return new List<CollectionComboOptions>();
                }

                var dynamicCombos = new List<CollectionComboOptions>();
                
                // Converter cada combo do servidor para o formato do client-side
                foreach (var serverCombo in serverCombos)
                {
                    try
                    {
                        var clientCombo = ConvertServerComboToClientCombo(serverCombo);
                        
                        // Definir o preço dinâmico (converter de centavos/100 fotos para reais/1000 fotos)
                        double priceFor1000Photos = (serverCombo.Price / 100.0) * 1000.0;
                        clientCombo.SetDynamicPrice(priceFor1000Photos);
                        
                        dynamicCombos.Add(clientCombo);
                    }
                    catch (Exception ex)
                    {
                        // Continuar com os outros combos em caso de erro
                    }
                }
                return dynamicCombos;
            }
            catch (Exception ex)
            {
                return new List<CollectionComboOptions>();
            }
        }

        /// <summary>
        /// Limpa o cache de combos
        /// </summary>
        public static void ClearCache()
        {
            _cachedCombos = null;
            _lastFetchTime = DateTime.MinValue;
        }

        /// <summary>
        /// Obtém o código do idioma atual para enviar à API
        /// </summary>
        private static string GetCurrentLanguageCode()
        {
            try
            {
                // Obter o idioma atual das configurações
                string currentLanguage = ViewModels.GlobalAppStateViewModel.options?.Language ?? "en-US";
                
                // Converter para o formato esperado pela API
                if (currentLanguage.StartsWith("pt"))
                {
                    return "pt";
                }
                else
                {
                    return "en";
                }
            }
            catch (Exception ex)
            {
                return "en"; // Fallback para inglês
            }
        }
    }
}
