using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LesserDashboardClient.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class CollectionCreationSupportDialog : Window
{
    private const string SupportWhatsAppNumber = "5518996880201";

    public CollectionCreationSupportDialog()
    {
        InitializeComponent();
        // Fechar só pelo X da janela — não dispensar com Enter/Escape como um OK implícito
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                e.Handled = true;
        };
    }

    /// <summary>Define o texto devolvido pelo servidor (mensagem completa, incluindo identificação da turma).</summary>
    public void SetMessage(string serverMessage)
    {
        DetailMessageText.Text = string.IsNullOrWhiteSpace(serverMessage)
            ? "Não há detalhes adicionais do servidor."
            : serverMessage.Trim();
    }

    private void BtWhatsApp_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string company = "";
            try
            {
                company = GlobalAppStateViewModel.lfc?.loginResult?.User?.company ?? "";
            }
            catch
            {
                /* ignore */
            }

            var detail = DetailMessageText.Text ?? "";
            var collectionLabel = GetCollectionLabelForWhatsApp(detail);
            var text =
                $"Olá! Houve um erro e preciso de ajuda com o processamento/criação da coleção no Lesser Dashboard. Empresa: {company}. Coleção: {collectionLabel}. Poderia me ajudar?";
            var url = "https://wa.me/" + SupportWhatsAppNumber + "?text=" + Uri.EscapeDataString(text);
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CollectionCreationSupportDialog WhatsApp: {ex.Message}");
        }
    }

    /// <summary>Prioriza o identificador da turma (ex.: antes de “Entre em contato” ou primeira linha).</summary>
    private static string GetCollectionLabelForWhatsApp(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return "";
        var t = detail.Trim();
        var idx = t.IndexOf(". Entre em contato", StringComparison.OrdinalIgnoreCase);
        if (idx > 0)
            return t[..idx].Trim();
        var line = t.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).FirstOrDefault()?.Trim();
        return string.IsNullOrEmpty(line) ? t : line;
    }
}
