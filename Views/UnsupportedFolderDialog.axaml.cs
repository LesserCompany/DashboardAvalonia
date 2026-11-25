using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using SharedClientSide_AVALONIA.Helpers;

namespace LesserDashboardClient.Views;

/// <summary>Aviso de pasta no OneDrive/Google Drive. O botão principal (ex.: "Usar C:\LetsPic") é opcional.</summary>
public partial class UnsupportedFolderDialog : Window
{
    public enum Choice { Closed, Primary, Secondary }

    public Choice Result { get; private set; } = Choice.Closed;

    public string Heading { get; } = string.Empty;
    public string FolderPath { get; } = string.Empty;
    public string Explanation { get; } = string.Empty;
    public string Advice { get; } = string.Empty;
    public string PrimaryButtonText { get; } = string.Empty;
    public string SecondaryButtonText { get; } = "Entendi";
    public bool HasPrimaryButton => !string.IsNullOrWhiteSpace(PrimaryButtonText);

    public RelayCommand PrimaryCommand { get; }
    public RelayCommand SecondaryCommand { get; }

    // Construtor sem parâmetros exigido pelo carregador de XAML/designer do Avalonia.
    public UnsupportedFolderDialog()
    {
        InitializeComponent();
        PrimaryCommand = new RelayCommand(() => CloseWith(Choice.Primary));
        SecondaryCommand = new RelayCommand(() => CloseWith(Choice.Secondary));
        DataContext = this;
    }

    public UnsupportedFolderDialog(FolderCheckResult blockedFolder, FolderRole role,
        string? primaryButtonText, string secondaryButtonText) : this()
    {
        Heading = CloudFolderGuard.BuildTitle(blockedFolder, role);
        FolderPath = blockedFolder.Path;
        Explanation = CloudFolderGuard.BuildExplanation(role);
        Advice = CloudFolderGuard.BuildAdvice(role);
        PrimaryButtonText = primaryButtonText ?? string.Empty;
        SecondaryButtonText = secondaryButtonText;

        // As propriedades acima não notificam mudança: religa o DataContext para as bindings lerem os valores.
        DataContext = null;
        DataContext = this;
    }

    private void CloseWith(Choice choice)
    {
        Result = choice;
        Close();
    }
}
