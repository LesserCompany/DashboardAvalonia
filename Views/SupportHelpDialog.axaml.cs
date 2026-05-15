using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LesserDashboardClient.ViewModels;
using System;
using System.Diagnostics;

namespace LesserDashboardClient.Views;

public partial class SupportHelpDialog : Window
{
    /// <summary>Central de tutoriais LetsPic no site.</summary>
    public const string TutorialsPageUrl = "https://www.lesser.biz/lesser-system-helpers/tutoriais-page/";

    private const string SupportWhatsAppNumber = "5518996880201";

    public SupportHelpDialog()
    {
        InitializeComponent();
        TutorialsUrlText.Text = TutorialsPageUrl;
        SupportPhoneText.Text = "+55 (18) 99688-0201";
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    private void BtClose_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void BtOpenTutorials_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = TutorialsPageUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SupportHelpDialog tutorials URL: {ex.Message}");
        }
    }

    private void BtWhatsApp_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string companyName = "";
            try
            {
                companyName = GlobalAppStateViewModel.lfc?.loginResult?.User?.company ?? "";
            }
            catch
            {
                /* ignore */
            }

            var url =
                $"https://wa.me/{SupportWhatsAppNumber}?text=" +
                Uri.EscapeDataString(
                    $"Olá! Falo em nome da empresa {companyName} e preciso de ajuda.");
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SupportHelpDialog WhatsApp: {ex.Message}");
        }
    }
}
