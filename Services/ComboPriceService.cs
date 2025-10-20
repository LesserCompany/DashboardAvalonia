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
                Console.WriteLine("ComboPriceService: Usando combos do cache");
                return _cachedCombos;
            }

            try
            {
                Console.WriteLine("ComboPriceService: Buscando combos do servidor...");
                
                if (ViewModels.GlobalAppStateViewModel.lfc == null)
                {
                    Console.WriteLine("ComboPriceService: LesserFunctionClient não está inicializado");
                    throw new Exception("LesserFunctionClient não está inicializado");
                }

                Console.WriteLine("ComboPriceService: Chamando API GetCompanyComboPrices...");
                
                // Obter o idioma atual das configurações
                string currentLanguage = GetCurrentLanguageCode();
                
                // Usar o LesserFunctionClient seguindo o padrão do projeto
                var result = await ViewModels.GlobalAppStateViewModel.lfc.GetCompanyComboPrices(currentLanguage);
                
                if (!result.success)
                {
                    Console.WriteLine($"ComboPriceService: Erro na API: {result.message}");
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
                            Console.WriteLine($"Erro ao converter combo: {ex.Message}");
                        }
                    }
                }
                
                Console.WriteLine($"ComboPriceService: {serverCombos.Count} combos obtidos com sucesso");

                _cachedCombos = serverCombos;
                _lastFetchTime = now;
                
                Console.WriteLine($"ComboPriceService: {_cachedCombos.Count} combos obtidos com sucesso");
                foreach (var combo in _cachedCombos)
                {
                    string currencySymbol = GetCurrencySymbol(combo.Coin);
                    Console.WriteLine($"  - {combo.ComboName}: {currencySymbol} {combo.Price:F4} para 100 fotos");
                }
                
                return _cachedCombos;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter combos do servidor: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
                Console.WriteLine("ComboPriceService: Criando combos dinamicamente do servidor...");
                
                // Buscar todos os combos do servidor
                var serverCombos = await GetCompanyComboPricesAsync();
                
                if (serverCombos == null || serverCombos.Count == 0)
                {
                    Console.WriteLine("ComboPriceService: Nenhum combo retornado do servidor, retornando lista vazia");
                    return new List<CollectionComboOptions>();
                }

                var dynamicCombos = new List<CollectionComboOptions>();
                
                // Converter cada combo do servidor para o formato do client-side
                foreach (var serverCombo in serverCombos)
                {
                    try
                    {
                        Console.WriteLine($"ComboPriceService: Convertendo combo '{serverCombo.ComboName}'");
                        
                        var clientCombo = ConvertServerComboToClientCombo(serverCombo);
                        
                        // Definir o preço dinâmico (converter de centavos/100 fotos para reais/1000 fotos)
                        double priceFor1000Photos = (serverCombo.Price / 100.0) * 1000.0;
                        clientCombo.SetDynamicPrice(priceFor1000Photos);
                        
                        dynamicCombos.Add(clientCombo);
                        
                        string currencySymbol = GetCurrencySymbol(serverCombo.Coin);
                        Console.WriteLine($"ComboPriceService: Combo '{serverCombo.ComboName}' criado com preço {currencySymbol} {priceFor1000Photos:F4}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao converter combo '{serverCombo.ComboName}': {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        // Continuar com os outros combos em caso de erro
                    }
                }
                
                Console.WriteLine($"ComboPriceService: {dynamicCombos.Count} combos dinâmicos criados com sucesso");
                return dynamicCombos;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro geral ao criar combos dinâmicos: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
            Console.WriteLine("ComboPriceService: Cache limpo");
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
                Console.WriteLine($"Erro ao obter idioma atual: {ex.Message}");
                return "en"; // Fallback para inglês
            }
        }
    }
}
