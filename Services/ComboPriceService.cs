using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Companies;
using LesserDashboardClient.Models;
using Newtonsoft.Json;

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
                
                // TEMPORÁRIO: Chamada direta à API até resolver problema de compilação
                var serverCombos = await CallGetCompanyComboPricesDirectly();
                
                Console.WriteLine($"ComboPriceService: {serverCombos.Count} combos obtidos com sucesso");

                _cachedCombos = serverCombos;
                _lastFetchTime = now;
                
                Console.WriteLine($"ComboPriceService: {_cachedCombos.Count} combos obtidos com sucesso");
                foreach (var combo in _cachedCombos)
                {
                    Console.WriteLine($"  - {combo.ComboName}: R$ {combo.Price:F4} para 100 fotos");
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
                IsTreatmentOnly = IsTreatmentOnlyCombo(serverCombo.ComboName)
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
                        
                        Console.WriteLine($"ComboPriceService: Combo '{serverCombo.ComboName}' criado com preço R$ {priceFor1000Photos:F4}");
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
        /// TEMPORÁRIO: Chamada direta à API GetCompanyComboPrices
        /// </summary>
        private static async Task<List<ServerCombo>> CallGetCompanyComboPricesDirectly()
        {
            try
            {
                Console.WriteLine("ComboPriceService: Fazendo chamada direta à API...");
                
                if (ViewModels.GlobalAppStateViewModel.lfc?.loginResult?.User?.loginToken == null)
                {
                    Console.WriteLine("ComboPriceService: Token de login não disponível");
                    throw new Exception("Token de login não disponível");
                }

                var token = ViewModels.GlobalAppStateViewModel.lfc.loginResult.User.loginToken;
                var payload = new { Token = token };
                var jsonPayload = JsonConvert.SerializeObject(payload);
                
                Console.WriteLine($"ComboPriceService: Payload: {jsonPayload}");
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                
                var endpoint = "https://new-functions-dev-exgkfwchaxagbnay.brazilsouth-01.azurewebsites.net/api/GetCompanyComboPrices";
                Console.WriteLine($"ComboPriceService: Endpoint: {endpoint}");
                
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(endpoint, content);
                
                Console.WriteLine($"ComboPriceService: Status Code: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"ComboPriceService: Erro na resposta: {errorContent}");
                    throw new Exception($"Erro na API: {response.StatusCode}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"ComboPriceService: Resposta recebida: {responseContent}");
                
                // Deserializar a resposta
                var apiResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                
                if (apiResponse?.success != true || apiResponse?.content == null)
                {
                    Console.WriteLine($"ComboPriceService: API retornou success=false ou content=null");
                    throw new Exception("API retornou resposta inválida");
                }
                
                // Converter content para List<ServerCombo>
                var serverCombos = new List<ServerCombo>();
                var contentArray = apiResponse.content as Newtonsoft.Json.Linq.JArray;
                
                if (contentArray != null)
                {
                    foreach (var item in contentArray)
                    {
                        try
                        {
                            var serverCombo = item.ToObject<ServerCombo>();
                            if (serverCombo != null)
                            {
                                serverCombos.Add(serverCombo);
                                Console.WriteLine($"ComboPriceService: Combo '{serverCombo.ComboName}' convertido com sucesso");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao converter combo: {ex.Message}");
                        }
                    }
                }
                
                Console.WriteLine($"ComboPriceService: {serverCombos.Count} combos convertidos com sucesso");
                return serverCombos;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro na chamada direta à API: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
