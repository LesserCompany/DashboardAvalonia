using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodingSeb.Localization;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LesserDashboardClient.ViewModels.Collections
{
    // Classe para o resultado do cálculo de preço
    public class PriceCalculationResult
    {
        [JsonProperty("value")]
        public double Value { get; set; }
        
        [JsonProperty("addedDays")]
        public int AddedDays { get; set; }
    }

    public partial class ChangeDeletionDateViewModel : ViewModelBase
    {
        private readonly LesserFunctionClient _lesserFunctionClient;
        private readonly ProfessionalTask _selectedCollection;
        private readonly Action _onSuccess;
        private Window? _window;

        [ObservableProperty]
        private DateTimeOffset? selectedDate;

        [ObservableProperty]
        private bool isDatePickerEnabled = true;

        [ObservableProperty]
        private bool isConfirmButtonEnabled = false;

        [ObservableProperty]
        private bool isLoadingPrice = false;

        [ObservableProperty]
        private string? priceText;

        [ObservableProperty]
        private bool isUpdating = false;

        [ObservableProperty]
        private bool showSuccessMessage = false;

        [ObservableProperty]
        private string? errorMessage;

        public ChangeDeletionDateViewModel(LesserFunctionClient lesserFunctionClient, ProfessionalTask selectedCollection, Action onSuccess, Window? window = null)
        {
            _lesserFunctionClient = lesserFunctionClient;
            _selectedCollection = selectedCollection;
            _onSuccess = onSuccess;
            _window = window;
            
            // Inicializa com 1 dia após a data de deleção agendada se existir
            var now = DateTimeOffset.Now;
            if (selectedCollection.ScheduledDeletionDate.HasValue && selectedCollection.ScheduledDeletionDate.Value > now)
            {
                // Define a data inicial como 1 dia após a data de deleção agendada
                SelectedDate = selectedCollection.ScheduledDeletionDate.Value.AddDays(1);
            }
            else
            {
                // Se não houver data ou a data for inválida, usa uma data padrão (ex: 1 ano a partir de agora)
                SelectedDate = new DateTimeOffset(DateTime.Now.AddYears(1));
            }
            
            // Inicializa o texto do preço
            PriceText = Loc.Tr("Loading price...");
        }

        public void SetWindow(Window window)
        {
            _window = window;
        }

        public async Task LoadInitialPriceAsync()
        {
            // Carrega o preço inicial quando o modal estiver totalmente carregado
            // Aguarda um pouco para garantir que o modal está totalmente renderizado
            // e evitar chamadas muito rápidas que podem causar problemas de autenticação
            await Task.Delay(300);
            
            // Verifica novamente se ainda temos uma data válida antes de carregar
            if (SelectedDate.HasValue && _selectedCollection != null && !string.IsNullOrEmpty(_selectedCollection.classCode))
            {
                await LoadPriceAsync();
            }
        }

        partial void OnSelectedDateChanged(DateTimeOffset? value)
        {
            if (value.HasValue && !IsUpdating)
            {
                // Valida se a data selecionada é maior que a data atual
                var now = DateTimeOffset.Now;
                if (value.Value <= now)
                {
                    ErrorMessage = Loc.Tr("The selected date must be greater than the current date.");
                    PriceText = "---";
                    IsConfirmButtonEnabled = false;
                    return;
                }
                
                // Limpa o erro se a data for válida
                ErrorMessage = null;
                _ = LoadPriceAsync();
            }
        }

        private async Task LoadPriceAsync()
        {
            if (!SelectedDate.HasValue || _selectedCollection == null || string.IsNullOrEmpty(_selectedCollection.classCode))
            {
                return;
            }

            // Valida se a data selecionada é maior que a data atual
            var now = DateTimeOffset.Now;
            if (SelectedDate.Value <= now)
            {
                ErrorMessage = Loc.Tr("The selected date must be greater than the current date.");
                PriceText = "---";
                IsConfirmButtonEnabled = false;
                return;
            }
            
            try
            {
                IsLoadingPrice = true;
                IsDatePickerEnabled = false;
                IsConfirmButtonEnabled = false;
                ErrorMessage = null;
                PriceText = Loc.Tr("Loading price...");

                // Verifica se o token está válido antes de chamar
                if (_lesserFunctionClient?.loginResult?.User == null)
                {
                    ErrorMessage = "Usuário não autenticado";
                    return;
                }

                // Converte para DateTimeOffset UTC no formato ISO 8601 padrão
                var dateTimeOffset = SelectedDate.Value.ToUniversalTime();
                
                var result = await _lesserFunctionClient.SimulateDeletionDeadlineExtensionCollectionPrice(
                    _selectedCollection.classCode,
                    dateTimeOffset
                );

                // Verifica se houve erro de autenticação REAL (não erro de cálculo de preço)
                if (result != null && result.loginFailed == true)
                {
                    // Se a mensagem for sobre cálculo de preço, não é erro de autenticação
                    bool isPriceCalculationError = result.message != null && 
                        (result.message.Contains("calcular o preço") || 
                         result.message.Contains("Não foi possível calcular") ||
                         result.message.Contains("calculate the price") ||
                         result.message.Contains("Unable to calculate"));
                    
                    if (isPriceCalculationError)
                    {
                        ErrorMessage = result.message ?? Loc.Tr("Error loading price");
                        PriceText = "---";
                        IsConfirmButtonEnabled = false;
                        return;
                    }
                    else
                    {
                        // Se o login falhou de verdade, não mostra erro - apenas desabilita
                        // O sistema já vai redirecionar para login automaticamente
                        ErrorMessage = null;
                        PriceText = Loc.Tr("Loading price...");
                        IsConfirmButtonEnabled = false;
                        return; // Sai sem habilitar o DatePicker para evitar novas tentativas
                    }
                }

                if (result != null && result.success == true && result.Content != null)
                {
                    // Deserializa o Content como PriceCalculationResult
                    var contentJson = JsonConvert.SerializeObject(result.Content);
                    var priceResult = JsonConvert.DeserializeObject<PriceCalculationResult>(contentJson);
                    
                    if (priceResult != null)
                    {
                        // Dividir por 100 para converter centavos em reais
                        double priceInReais = priceResult.Value / 100.0;
                        PriceText = $"R$ {priceInReais:F2}";
                        IsConfirmButtonEnabled = true;
                    }
                    else
                    {
                        ErrorMessage = Loc.Tr("Error loading price");
                        PriceText = "---";
                        IsConfirmButtonEnabled = false;
                    }
                }
                else
                {
                    ErrorMessage = result?.message ?? Loc.Tr("Error loading price");
                    PriceText = "---";
                    IsConfirmButtonEnabled = false;
                }
            }
            catch (Exception ex)
            {
                // Não mostra erro de autenticação como erro fatal - apenas desabilita o botão
                if (ex.Message.Contains("session") || ex.Message.Contains("token") || ex.Message.Contains("login"))
                {
                    ErrorMessage = null; // Não mostra erro para não assustar o usuário
                    PriceText = Loc.Tr("Loading price...");
                }
                else
                {
                    ErrorMessage = ex.Message;
                    PriceText = Loc.Tr("Loading price...");
                }
                IsConfirmButtonEnabled = false;
            }
            finally
            {
                IsLoadingPrice = false;
                IsDatePickerEnabled = true;
            }
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (!SelectedDate.HasValue || _selectedCollection == null || string.IsNullOrEmpty(_selectedCollection.classCode))
            {
                return;
            }

            // Valida se a data selecionada é maior que a data atual
            var now = DateTimeOffset.Now;
            if (SelectedDate.Value <= now)
            {
                ErrorMessage = Loc.Tr("The selected date must be greater than the current date.");
                return;
            }

            try
            {
                IsUpdating = true;
                IsDatePickerEnabled = false;
                IsConfirmButtonEnabled = false;
                ErrorMessage = null;

                // Verifica se o token está válido antes de chamar
                if (_lesserFunctionClient?.loginResult?.User == null)
                {
                    ErrorMessage = "Usuário não autenticado";
                    return;
                }

                // Converte para DateTimeOffset UTC no formato ISO 8601 padrão
                var dateTimeOffset = SelectedDate.Value.ToUniversalTime();
                
                var result = await _lesserFunctionClient.UpdateDeletionDeadlineExtensionCollection(
                    _selectedCollection.classCode,
                    dateTimeOffset
                );

                if (result != null && result.success == true)
                {
                    ShowSuccessMessage = true;
                    
                    // Aguarda um pouco antes de fechar e chamar o callback
                    await Task.Delay(1500);
                    
                    _onSuccess?.Invoke();
                    
                    // Fecha a janela
                    if (_window != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => _window.Close());
                    }
                }
                else
                {
                    ErrorMessage = result?.message ?? Loc.Tr("Error updating deletion date");
                    IsDatePickerEnabled = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                IsDatePickerEnabled = true;
            }
            finally
            {
                IsUpdating = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (_window != null)
            {
                Dispatcher.UIThread.Post(() => _window.Close());
            }
        }
    }
}

