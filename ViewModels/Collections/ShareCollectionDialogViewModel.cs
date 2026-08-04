using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.Models;
using LesserDashboardClient.Services;
using SharedClientSide.ServerInteraction.Users.Companies;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Collections;

public partial class ShareCollectionDialogViewModel : ObservableObject
{
    private readonly string _classCode;
    private readonly string _companyUsername;

    [ObservableProperty] private string shareLink = string.Empty;
    [ObservableProperty] private Bitmap? qrCodeImage;
    [ObservableProperty] private bool linkCopied;
    [ObservableProperty] private ShareLinkBaseOption? selectedBaseOption;

    public ObservableCollection<ShareLinkBaseOption> BaseOptions { get; } = new();

    public bool IsBaseSelectorVisible => BaseOptions.Count > 1;

    public ShareCollectionDialogViewModel(string classCode, string companyUsername, Company? company)
    {
        _classCode = classCode ?? throw new ArgumentNullException(nameof(classCode));
        _companyUsername = companyUsername ?? throw new ArgumentNullException(nameof(companyUsername));

        foreach (var (customDomain, displayHost) in CollectionViewerLinkService.EnumerateShareLinkBases(company))
        {
            var label = customDomain == null
                ? Loc.Tr("Default viewer (visualizador.lesser.biz)", "Visualizador — visualizador.lesser.biz (padrão)")
                : displayHost;

            BaseOptions.Add(new ShareLinkBaseOption
            {
                CustomDomain = customDomain,
                DisplayLabel = label
            });
        }

        SelectedBaseOption = BaseOptions.Count > 0 ? BaseOptions[0] : null;
        OnPropertyChanged(nameof(IsBaseSelectorVisible));
        RefreshLinkAndQrCode();
    }

    partial void OnSelectedBaseOptionChanged(ShareLinkBaseOption? value)
    {
        LinkCopied = false;
        RefreshLinkAndQrCode();
    }

    private void RefreshLinkAndQrCode()
    {
        if (SelectedBaseOption == null)
        {
            ShareLink = string.Empty;
            ReplaceQrCode(null);
            return;
        }

        ShareLink = CollectionViewerLinkService.BuildCollectionShareLink(
            _classCode,
            _companyUsername,
            SelectedBaseOption.CustomDomain);

        ReplaceQrCode(string.IsNullOrWhiteSpace(ShareLink) ? null : QrCodeBitmapHelper.CreateQrBitmap(ShareLink));
    }

    private void ReplaceQrCode(Bitmap? newBitmap)
    {
        var previous = QrCodeImage;
        QrCodeImage = newBitmap;
        previous?.Dispose();
    }

    [RelayCommand]
    private async Task CopyLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(ShareLink))
            return;

        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.Clipboard != null)
                await topLevel.Clipboard.SetTextAsync(ShareLink);

            LinkCopied = true;
            await Task.Delay(2000);
            LinkCopied = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CopyLinkAsync: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenLink()
    {
        if (string.IsNullOrWhiteSpace(ShareLink))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(ShareLink) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenLink: {ex.Message}");
        }
    }

    private static TopLevel? GetTopLevel()
    {
        return Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
            _ => null
        };
    }
}
