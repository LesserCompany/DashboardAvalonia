using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CodingSeb.Localization;
using LesserDashboardClient.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LesserDashboardClient.Views;

/// <summary>
/// Notas de atualização desta versão do dashboard, vindas do servidor (GetAppReleaseNotes) — ver
/// Helpers/ReleaseNotesTarget pra qual app/versão é pedida (instalação em partes ou pacote da loja).
/// </summary>
public partial class ReleaseNotesDialog : Window
{
    private readonly string _appName = "";
    private readonly string _version = "";

    // Construtor sem parâmetros só pro designer do Avalonia.
    public ReleaseNotesDialog()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    public ReleaseNotesDialog(string appName, string version) : this()
    {
        _appName = appName;
        _version = version;
        VersionText.Text = string.IsNullOrWhiteSpace(version) ? "" : $"{Loc.Tr("Release notes version")} {DisplayVersion(version)}";
        Opened += async (_, _) => await LoadAsync();
    }

    private static string DisplayVersion(string version) =>
        version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : "v" + version;

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_appName) || string.IsNullOrWhiteSpace(_version))
        {
            ShowMessage(Loc.Tr("Release notes empty"));
            return;
        }

        try
        {
            var result = await GlobalAppStateViewModel.lfc.GetAppReleaseNotes(_appName, _version);
            if (result?.success != true)
            {
                ShowMessage(Loc.Tr("Release notes error"));
                return;
            }

            var note = result.Content;
            if (note == null || string.IsNullOrWhiteSpace(note.Notes))
            {
                ShowMessage(Loc.Tr("Release notes empty"));
                return;
            }

            if (!string.IsNullOrWhiteSpace(note.Title))
                TitleText.Text = note.Title;
            NotesText.Text = note.Notes.Replace("\r\n", "\n").Trim();
            LoadingPanel.IsVisible = false;
            NotesScroll.IsVisible = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ReleaseNotesDialog: {ex.Message}");
            ShowMessage(Loc.Tr("Release notes error"));
        }
    }

    private void ShowMessage(string message)
    {
        LoadingPanel.IsVisible = false;
        NotesScroll.IsVisible = false;
        MessageText.Text = message;
        MessageText.IsVisible = true;
    }

    private void BtClose_OnClick(object? sender, RoutedEventArgs e) => Close();
}
