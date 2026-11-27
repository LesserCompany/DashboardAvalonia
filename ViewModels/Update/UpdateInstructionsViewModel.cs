using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.Helpers;
using LesserDashboardClient.Services.Store;
using SharedClientSide.Helpers;

namespace LesserDashboardClient.ViewModels.Update;

public sealed class UpdateStepViewModel
{
    public UpdateStepViewModel(UpdateStep step, bool highlighted, IRelayCommand command)
    {
        Number = step.Number.ToString();
        Title = step.Title;
        Detail = step.Detail;
        ActionLabel = step.ActionLabel ?? string.Empty;
        HasAction = step.HasAction;
        IsPrimaryAction = step.IsPrimaryAction;
        IsSecondaryAction = step.HasAction && !step.IsPrimaryAction;
        IsHighlighted = highlighted;
        Command = command;
    }

    public string Number { get; }
    public string Title { get; }
    public string Detail { get; }
    public string ActionLabel { get; }
    public bool HasAction { get; }
    public bool IsPrimaryAction { get; }
    public bool IsSecondaryAction { get; }
    public bool IsHighlighted { get; }
    public IRelayCommand Command { get; }
}

/// <summary>Tela de instruções de atualização (modo Store ou modo reinstalar pelo letspic.zip).</summary>
public partial class UpdateInstructionsViewModel : ViewModelBase
{
    private readonly StoreUpdateService _store;

    public UpdateInstructionsViewModel(UpdateInstructionsDecision decision, string installedVersion, StoreUpdateService store)
    {
        _store = store;
        Decision = decision;
        UpdateInstructionsContent content = UpdateInstructionsContentFactory.For(decision);

        Label = content.Label;
        Headline = content.Headline;
        Explanation = content.Explanation;
        StepsTitle = content.StepsTitle;
        DismissLabel = content.DismissLabel;
        DismissNote = content.DismissNote ?? string.Empty;
        InstalledVersionText = string.IsNullOrWhiteSpace(installedVersion) ? string.Empty : $"Versão instalada: {installedVersion}";

        bool highlightSteps = decision.Mode == UpdateInstructionsMode.ReinstallFromZip;
        Steps = content.Steps
            .Select(s => new UpdateStepViewModel(s, highlightSteps, new RelayCommand(() => RunStep(s.Action))))
            .ToList();
    }

    // Construtor do designer do Avalonia.
    public UpdateInstructionsViewModel()
        : this(new UpdateInstructionsDecision(UpdateInstructionsMode.StoreUpdate, true), "1.0.22.0", StoreUpdateService.Instance)
    {
    }

    public UpdateInstructionsDecision Decision { get; }
    public bool IsStoreMode => Decision.Mode == UpdateInstructionsMode.StoreUpdate;
    public bool IsZipMode => Decision.Mode == UpdateInstructionsMode.ReinstallFromZip;
    public bool IsBlocking => Decision.IsBlocking;

    public string Label { get; }
    public string Headline { get; }
    public string Explanation { get; }
    public string StepsTitle { get; }
    public string DismissLabel { get; }
    public string DismissNote { get; }
    public bool HasDismissNote => !string.IsNullOrWhiteSpace(DismissNote);
    public string InstalledVersionText { get; }
    public bool HasInstalledVersion => !string.IsNullOrWhiteSpace(InstalledVersionText);
    public IReadOnlyList<UpdateStepViewModel> Steps { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdateNow))]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _hasInstallProgress;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstallStatus))]
    private string _installStatus = string.Empty;

    public bool HasInstallStatus => !string.IsNullOrWhiteSpace(InstallStatus);
    public bool CanUpdateNow => !IsInstalling;

    /// <summary>Handle da janela dona, exigido pela Store para mostrar o diálogo dela em app desktop.</summary>
    public Func<IntPtr>? OwnerWindowHandle { get; set; }

    public event EventHandler? CloseRequested;

    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        if (IsInstalling)
            return;

        IsInstalling = true;
        HasInstallProgress = false;
        InstallProgress = 0;
        InstallStatus = "Abrindo a Microsoft Store…";

        StoreInstallOutcome outcome = await _store.InstallStoreUpdatesAsync(
            OwnerWindowHandle?.Invoke() ?? IntPtr.Zero,
            p => Dispatcher.UIThread.Post(() =>
            {
                HasInstallProgress = true;
                InstallProgress = Math.Clamp(p, 0, 1) * 100;
                InstallStatus = $"Baixando e instalando… {InstallProgress:0}%";
            }));

        switch (outcome)
        {
            case StoreInstallOutcome.Completed:
                InstallStatus = "Atualização instalada. Se o LetsPic não fechar sozinho, feche e abra de novo.";
                break;
            case StoreInstallOutcome.Canceled:
                IsInstalling = false;
                HasInstallProgress = false;
                InstallStatus = "Atualização cancelada. Clique em Atualizar agora quando quiser.";
                break;
            case StoreInstallOutcome.NothingToInstall:
                IsInstalling = false;
                HasInstallProgress = false;
                InstallStatus = "A Store ainda não liberou a versão nova aqui. Abrimos a página do LetsPic: clique em Atualizar lá.";
                OpenStore();
                break;
            default:
                IsInstalling = false;
                HasInstallProgress = false;
                InstallStatus = "Não deu para atualizar por aqui. Abrimos a Microsoft Store: clique em Atualizar lá.";
                OpenStore();
                break;
        }
    }

    [RelayCommand]
    private void OpenStore() => Launch(UpdateLinks.StoreProductPageUri);

    [RelayCommand]
    private void Dismiss() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void RunStep(UpdateStepAction action)
    {
        switch (action)
        {
            case UpdateStepAction.OpenStore: Launch(UpdateLinks.StoreProductPageUri); break;
            case UpdateStepAction.OpenInstalledApps: Launch(UpdateLinks.InstalledAppsSettingsUri); break;
            case UpdateStepAction.DownloadZip: Launch(UpdateLinks.ZipDownloadUrl); break;
        }
    }

    private static void Launch(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            PackagedAppHelper.MsixLog($"UpdateInstructions: abrir '{uri}' falhou: {ex.Message}");
        }
    }
}
