using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodingSeb.Localization;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users;
using SharedClientSide.ServerInteraction.Users.Companies.Results;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Collections
{
    public partial class ChangeDeletionDateViewModel : ViewModelBase
    {
        /// <summary>Mínimo no campo <see cref="ExtendScheduledDeletionDateSimulationResult.TotalValue"/> (centavos de real) para cobrança (R$ 0,10).</summary>
        private const double MinimumExtensionTotalApiCentavos = 10;

        private readonly LesserFunctionClient _lesserFunctionClient;
        private readonly ProfessionalTask _selectedCollection;
        private readonly Action _onSuccess;
        private Window? _window;

        /// <summary>Último total simulado (API, centavos de real) para validar confirmação.</summary>
        private double _lastSuccessfulTotalValueCents;

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
        private bool showPriceBreakdown;

        [ObservableProperty]
        private string? breakdownDailyLine;

        [ObservableProperty]
        private string? breakdownDaysLine;

        [ObservableProperty]
        private string? breakdownPhotosLine;

        [ObservableProperty]
        private string? priceTotalLine;

        /// <summary>Aviso quando o total está abaixo do mínimo faturável (não é erro de rede).</summary>
        [ObservableProperty]
        private string? minimumInvoiceNotice;

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
                SelectedDate = selectedCollection.ScheduledDeletionDate.Value.AddDays(1);
            }
            else
            {
                SelectedDate = new DateTimeOffset(DateTime.Now.AddYears(1));
            }

            PriceText = Loc.Tr("Loading price...");
        }

        /// <summary>Mostra texto simples (carregando ou ---) quando não há detalhamento nem ring de carga.</summary>
        public bool ShowPricePlaceholder => !IsLoadingPrice && !ShowPriceBreakdown && !string.IsNullOrEmpty(PriceText);

        partial void OnIsLoadingPriceChanged(bool value) => OnPropertyChanged(nameof(ShowPricePlaceholder));

        partial void OnShowPriceBreakdownChanged(bool value) => OnPropertyChanged(nameof(ShowPricePlaceholder));

        partial void OnPriceTextChanged(string? value) => OnPropertyChanged(nameof(ShowPricePlaceholder));

        public void SetWindow(Window window)
        {
            _window = window;
        }

        public async Task LoadInitialPriceAsync()
        {
            await Task.Delay(300);

            if (SelectedDate.HasValue && _selectedCollection != null && !string.IsNullOrEmpty(_selectedCollection.classCode))
            {
                await LoadPriceAsync();
            }
        }

        partial void OnSelectedDateChanged(DateTimeOffset? value)
        {
            if (value.HasValue && !IsUpdating)
            {
                var now = DateTimeOffset.Now;
                if (value.Value <= now)
                {
                    ErrorMessage = Loc.Tr("The selected date must be greater than the current date.");
                    PriceText = "---";
                    ClearPriceBreakdown();
                    IsConfirmButtonEnabled = false;
                    return;
                }

                ErrorMessage = null;
                _ = LoadPriceAsync();
            }
        }

        private void ClearPriceBreakdown()
        {
            ShowPriceBreakdown = false;
            BreakdownDailyLine = null;
            BreakdownDaysLine = null;
            BreakdownPhotosLine = null;
            PriceTotalLine = null;
            MinimumInvoiceNotice = null;
            _lastSuccessfulTotalValueCents = 0;
        }

        private async Task LoadPriceAsync()
        {
            if (!SelectedDate.HasValue || _selectedCollection == null || string.IsNullOrEmpty(_selectedCollection.classCode))
            {
                return;
            }

            var now = DateTimeOffset.Now;
            if (SelectedDate.Value <= now)
            {
                ErrorMessage = Loc.Tr("The selected date must be greater than the current date.");
                PriceText = "---";
                ClearPriceBreakdown();
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
                ClearPriceBreakdown();

                if (_lesserFunctionClient?.loginResult?.User == null)
                {
                    ErrorMessage = "Usuário não autenticado";
                    return;
                }

                var oldDateTimeOffset = _selectedCollection.ScheduledDeletionDate ?? DateTimeOffset.Now;
                var newDateTimeOffset = SelectedDate.Value.ToUniversalTime().AddHours(5);

                var result = await _lesserFunctionClient.SimulateExtendScheduledDeletionDate(
                    _selectedCollection.classCode,
                    oldDateTimeOffset.ToUniversalTime(),
                    newDateTimeOffset,
                    false);

                if (result != null && result.loginFailed == true)
                {
                    bool isPriceCalculationError = result.message != null &&
                        (result.message.Contains("calcular o preço") ||
                         result.message.Contains("Não foi possível calcular") ||
                         result.message.Contains("calculate the price") ||
                         result.message.Contains("Unable to calculate"));

                    if (isPriceCalculationError)
                    {
                        ErrorMessage = result.message ?? Loc.Tr("Error loading price");
                        PriceText = "---";
                        ClearPriceBreakdown();
                        IsConfirmButtonEnabled = false;
                        return;
                    }

                    ErrorMessage = null;
                    PriceText = Loc.Tr("Loading price...");
                    ClearPriceBreakdown();
                    IsConfirmButtonEnabled = false;
                    return;
                }

                if (result != null && result.success == true && result.Content != null)
                {
                    ApplySuccessfulPriceQuote(result.Content);
                }
                else
                {
                    ErrorMessage = result?.message ?? Loc.Tr("Error loading price");
                    PriceText = "---";
                    ClearPriceBreakdown();
                    IsConfirmButtonEnabled = false;
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("session") || ex.Message.Contains("token") || ex.Message.Contains("login"))
                {
                    ErrorMessage = null;
                    PriceText = Loc.Tr("Loading price...");
                }
                else
                {
                    ErrorMessage = ex.Message;
                    PriceText = Loc.Tr("Loading price...");
                }

                IsConfirmButtonEnabled = false;
                ClearPriceBreakdown();
            }
            finally
            {
                IsLoadingPrice = false;
                IsDatePickerEnabled = true;
            }
        }

        private void ApplySuccessfulPriceQuote(ExtendScheduledDeletionDateSimulationResult content)
        {
            double totalPriceReais = content.TotalValue / 100.0;
            double dailyReais = content.DailyPricePerPhoto / 100.0;
            int days = content.AddedDays;

            BreakdownDailyLine = $"{Loc.Tr("Daily cost per photo:")} {FormatDailyReais(dailyReais)}";
            BreakdownDaysLine = $"{Loc.Tr("Added days:")} {days}";

            int? photos = ResolvePhotoCountForDisplay(content);
            BreakdownPhotosLine = photos.HasValue
                ? $"{Loc.Tr("Photos in collection:")} {photos.Value}"
                : Loc.Tr("Photos in collection (unknown).");

            PriceTotalLine = $"{Loc.Tr("Total:")} {FormatReais(totalPriceReais, 2)}";

            ShowPriceBreakdown = true;
            PriceText = null;

            _lastSuccessfulTotalValueCents = content.TotalValue;

            if (content.TotalValue < MinimumExtensionTotalApiCentavos)
            {
                MinimumInvoiceNotice = Loc.Tr(
                    "The extension must be at least R$ 0.10. Choose a longer storage period.",
                    "O valor da extensão precisa ser de no mínimo R$ 0,10. Aumente o período de armazenamento.");
                IsConfirmButtonEnabled = false;
            }
            else
            {
                MinimumInvoiceNotice = null;
                IsConfirmButtonEnabled = true;
            }
        }

        private int? ResolvePhotoCountForDisplay(ExtendScheduledDeletionDateSimulationResult content)
        {
            if (TryInferPhotoCount(content.TotalValue, content.PricePerPhoto, out int inferred))
                return inferred;

            int fromCollection = (_selectedCollection.recognitionPhotos ?? 0) + (_selectedCollection.eventPhotos ?? 0);
            return fromCollection > 0 ? fromCollection : null;
        }

        private static string FormatReais(double valueInReais, int decimalPlaces)
        {
            var fmt = decimalPlaces <= 0 ? "F0" : $"F{decimalPlaces}";
            var n = valueInReais.ToString(fmt, CultureInfo.InvariantCulture).Replace('.', ',');
            return $"R$ {n}";
        }

        private static string FormatDailyReais(double dailyInReais)
        {
            int decimals = dailyInReais is > 0 and < 0.01 ? 6 : 4;
            return FormatReais(dailyInReais, decimals);
        }

        private static bool TryInferPhotoCount(
            double totalValueCents,
            double pricePerPhotoCents,
            out int photoCount)
        {
            photoCount = 0;
            if (pricePerPhotoCents <= 0)
                return false;

            double ratio = totalValueCents / pricePerPhotoCents;
            int rounded = (int)Math.Round(ratio, MidpointRounding.AwayFromZero);
            if (rounded <= 0)
                return false;

            if (Math.Abs(ratio - rounded) > 0.02)
                return false;

            photoCount = rounded;
            return true;
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (!SelectedDate.HasValue || _selectedCollection == null || string.IsNullOrEmpty(_selectedCollection.classCode))
            {
                return;
            }

            if (_lastSuccessfulTotalValueCents < MinimumExtensionTotalApiCentavos)
            {
                ErrorMessage = Loc.Tr(
                    "The extension must be at least R$ 0.10. Choose a longer storage period.",
                    "O valor da extensão precisa ser de no mínimo R$ 0,10. Aumente o período de armazenamento.");
                return;
            }

            var now = DateTimeOffset.Now;
            if (SelectedDate.Value <= now)
            {
                ErrorMessage = Loc.Tr("The selected date must be greater than the current date.");
                return;
            }

            if (_lesserFunctionClient?.loginResult?.User == null)
            {
                ErrorMessage = "Usuário não autenticado";
                return;
            }

            try
            {
                IsUpdating = true;
                IsDatePickerEnabled = false;
                IsConfirmButtonEnabled = false;
                ErrorMessage = null;

                var dateTimeOffset = SelectedDate.Value.ToUniversalTime().AddHours(5);

                var result = await _lesserFunctionClient.UpdateDeletionDeadlineExtensionCollection(
                    _selectedCollection.classCode,
                    dateTimeOffset);

                if (result != null && result.success == true)
                {
                    ShowSuccessMessage = true;

                    await Task.Delay(1500);

                    _onSuccess?.Invoke();

                    if (_window != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => _window.Close());
                    }
                }
                else
                {
                    ErrorMessage = result?.message ?? Loc.Tr("Error updating deletion date");
                    IsDatePickerEnabled = true;
                    IsConfirmButtonEnabled = _lastSuccessfulTotalValueCents >= MinimumExtensionTotalApiCentavos && ShowPriceBreakdown;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                IsDatePickerEnabled = true;
                IsConfirmButtonEnabled = _lastSuccessfulTotalValueCents >= MinimumExtensionTotalApiCentavos && ShowPriceBreakdown;
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
