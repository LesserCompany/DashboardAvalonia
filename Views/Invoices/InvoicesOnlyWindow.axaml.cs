using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using SharedClientSide.Financial;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LesserDashboardClient.Views.Invoices
{
    public partial class InvoicesOnlyWindow : Window
    {
        public InvoicesOnlyWindow()
        {
            InitializeComponent();
            this.Loaded += InvoicesOnlyWindow_Loaded;
        }

        private async void InvoicesOnlyWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Invoices.InvoicesOnlyViewModel viewModel)
            {
                // Carrega as faturas quando a janela é carregada
                await viewModel.LoadInvoicesAsync();
            }
        }

        private async void CopyEmail_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (Clipboard != null)
                    await Clipboard.SetTextAsync("contato@lesser.biz");
            }
            catch { }
        }

        private async void PayButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Invoice invoice && DataContext is ViewModels.Invoices.InvoicesOnlyViewModel viewModel)
            {
                await viewModel.PayInvoice(invoice);
            }
        }
    }

    public class PaymentStatusToTextConverter : IValueConverter
    {
        public static readonly PaymentStatusToTextConverter Instance = new PaymentStatusToTextConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    Invoice.INVOICE_PAYMENT_STATUSES.PAID => "Pago",
                    Invoice.INVOICE_PAYMENT_STATUSES.PENDING => "Pendente",
                    _ => "Desconhecido"
                };
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PaymentStatusToCanPayConverter : IValueConverter
    {
        public static readonly PaymentStatusToCanPayConverter Instance = new PaymentStatusToCanPayConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status == Invoice.INVOICE_PAYMENT_STATUSES.PENDING;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BillingPeriodToCompetenciaConverter : IValueConverter
    {
        public static readonly BillingPeriodToCompetenciaConverter Instance = new BillingPeriodToCompetenciaConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTimeOffset date)
            {
                // Formato: M/YYYY (ex: 3/2023)
                return $"{date.Month}/{date.Year}";
            }
            else if (value is DateTime dateTime)
            {
                // Formato: M/YYYY (ex: 3/2023)
                return $"{dateTime.Month}/{dateTime.Year}";
            }
            return value?.ToString() ?? "-";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DateToLocalConverter : IValueConverter
    {
        public static readonly DateToLocalConverter Instance = new DateToLocalConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTimeOffset date)
            {
                // Converte para timezone local e formata
                return date.ToLocalTime().ToString("dd/MM/yyyy");
            }
            else if (value is DateTime dateTime)
            {
                return dateTime.ToString("dd/MM/yyyy");
            }
            return value?.ToString() ?? "-";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PaymentDateConverter : IMultiValueConverter
    {
        public static readonly PaymentDateConverter Instance = new PaymentDateConverter();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return "---";

            // Verifica se os valores são UnsetValue (valores não definidos no binding)
            if (values[0] == AvaloniaProperty.UnsetValue || values[1] == AvaloniaProperty.UnsetValue)
                return "---";

            var paymentStatus = values[0] as string;
            
            // Trata DateTimeOffset? que pode vir como DateTimeOffset ou null
            DateTimeOffset? dueDate = null;
            if (values[1] is DateTimeOffset dt)
            {
                // Converte para o timezone local para evitar problemas de data
                dueDate = dt.ToLocalTime();
            }
            else if (values[1] is DateTime dateTime)
            {
                dueDate = new DateTimeOffset(dateTime).ToLocalTime();
            }

            // Se está pago, mostra a data de vencimento (dueDate), senão mostra "---"
            // Nota: No Svelte usa dueDate quando pago, que corresponde ao pagamento efetivo
            if (paymentStatus == Invoice.INVOICE_PAYMENT_STATUSES.PAID && dueDate.HasValue)
            {
                return dueDate.Value.ToString("dd/MM/yyyy");
            }
            return "---";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
