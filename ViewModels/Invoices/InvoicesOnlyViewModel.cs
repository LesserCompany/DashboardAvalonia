using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedClientSide.Financial;
using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Invoices
{
    public partial class InvoicesOnlyViewModel : ViewModelBase
    {
        private readonly LesserFunctionClient _lfc;
        private readonly string _username;

        /// <summary>True quando o bloqueio é administrativo (admin_Blocked); exibe aviso para contatar o suporte e link WhatsApp.</summary>
        public bool IsAdminBlocked { get; }

        /// <summary>True quando o bloqueio é apenas por fatura pendente (sem bloqueio admin).</summary>
        public bool IsBlockedByInvoiceOnly => !IsAdminBlocked;

        [ObservableProperty]
        private ObservableCollection<Invoice> invoices = new ObservableCollection<Invoice>();

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private string errorMessage = "";

        [ObservableProperty]
        private bool hasError = false;

        public bool IsDarkMode => GlobalAppStateViewModel.Instance.AppIsDarkMode;

        public bool ShowDataGrid => !IsLoading && !HasError && Invoices.Count > 0;
        
        public bool ShowEmptyState => !IsLoading && !HasError && Invoices.Count == 0;

        public InvoicesOnlyViewModel(LesserFunctionClient lfc, string username, bool isAdminBlocked = false)
        {
            _lfc = lfc ?? throw new ArgumentNullException(nameof(lfc));
            _username = username ?? throw new ArgumentNullException(nameof(username));
            IsAdminBlocked = isAdminBlocked;
        }

        [RelayCommand]
        public async Task LoadInvoicesAsync()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsLoading = true;
                    HasError = false;
                    ErrorMessage = "";
                });

                var invoicesList = await _lfc.GetInvoices(null, _username);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Invoices.Clear();
                    // Ordena as faturas: mais novas primeiro (por competência descendente)
                    var sortedInvoices = (invoicesList ?? new List<Invoice>())
                        .OrderByDescending(i => i.BillingPeriodStartDate)
                        .ToList();
                    
                    foreach (var invoice in sortedInvoices)
                    {
                        Invoices.Add(invoice);
                    }
                    IsLoading = false;
                    OnPropertyChanged(nameof(ShowDataGrid));
                    OnPropertyChanged(nameof(ShowEmptyState));
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsLoading = false;
                    HasError = true;
                    ErrorMessage = $"Erro ao carregar faturas: {ex.Message}";
                    OnPropertyChanged(nameof(ShowDataGrid));
                    OnPropertyChanged(nameof(ShowEmptyState));
                });
            }
        }

        public string FormatCurrency(double? value)
        {
            if (value == null)
                return "R$ 0,00";
            return $"R$ {value.Value:F2}";
        }

        public string FormatDate(DateTimeOffset? date)
        {
            if (date == null)
                return "-";
            return date.Value.ToString("dd/MM/yyyy");
        }

        public string GetPaymentStatusText(string status)
        {
            return status switch
            {
                Invoice.INVOICE_PAYMENT_STATUSES.PAID => "Pago",
                Invoice.INVOICE_PAYMENT_STATUSES.PENDING => "Pendente",
                _ => "Desconhecido"
            };
        }

        [ObservableProperty]
        private Dictionary<Invoice, bool> invoiceLoadingStates = new Dictionary<Invoice, bool>();

        [RelayCommand]
        public async Task PayInvoice(Invoice invoice)
        {
            if (invoice == null)
                return;

            try
            {
                InvoiceLoadingStates[invoice] = true;
                OnPropertyChanged(nameof(InvoiceLoadingStates));

                // Obtém o link de pagamento válido usando o método ASAAS
                var paymentUrl = await _lfc.GetInvoiceWithValidASAASPaymentLinkAfterDueDate(invoice, null, _username);

                if (!string.IsNullOrEmpty(paymentUrl))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = paymentUrl,
                        UseShellExecute = true
                    });
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ErrorMessage = "Não foi possível gerar o link de pagamento. Por favor, entre em contato com o suporte.";
                        HasError = true;
                    });
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ErrorMessage = $"Erro ao gerar link de pagamento: {ex.Message}";
                    HasError = true;
                });
            }
            finally
            {
                InvoiceLoadingStates[invoice] = false;
                OnPropertyChanged(nameof(InvoiceLoadingStates));
            }
        }

        public bool IsInvoiceLoading(Invoice invoice)
        {
            return invoice != null && InvoiceLoadingStates.ContainsKey(invoice) && InvoiceLoadingStates[invoice];
        }

        public bool CanPayInvoice(Invoice invoice)
        {
            return invoice != null && invoice.PaymentStatus == Invoice.INVOICE_PAYMENT_STATUSES.PENDING;
        }

        [RelayCommand]
        public void OpenEmail()
        {
            try
            {
                var email = "contato@lesser.biz";
                var subject = "Consulta sobre faturas";
                var body = $"Olá,\n\nGostaria de consultar sobre minhas faturas.\n\nUsuário: {_username}\n\nAtenciosamente";
                
                var mailtoUrl = $"mailto:{email}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = mailtoUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao abrir email: {ex.Message}");
            }
        }

        private const string SupportWhatsAppUrl = "https://wa.me/5518996880201";
        private const string FinanceWhatsAppUrl = "https://wa.me/554491627057";

        [RelayCommand]
        public void OpenWhatsAppSupport()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SupportWhatsAppUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao abrir WhatsApp: {ex.Message}");
            }
        }

        [RelayCommand]
        public void OpenWhatsAppFinance()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = FinanceWhatsAppUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao abrir WhatsApp financeiro: {ex.Message}");
            }
        }
    }
}
