using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.Models;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient.ViewModels.Collections;

// Permissões (RBAC) do professional na tela "Adicionar novo profissional". Catálogo vem do servidor
// (GetUserAuthorizationOptions_v2); o que for marcado vai junto no RegisterProfessional_v2.
public partial class CollectionsViewModel
{
    [ObservableProperty] private ObservableCollection<ProfessionalRoleOption> newProfessionalRoles = new();
    [ObservableProperty] private ObservableCollection<ProfessionalPolicyDomainRow> newProfessionalPolicyDomains = new();
    [ObservableProperty] private bool newProfessionalPermissionsLoading;
    [ObservableProperty] private string? newProfessionalPermissionsError;

    public bool NewProfessionalPermissionsAvailable => NewProfessionalRoles.Count > 0 || NewProfessionalPolicyDomains.Count > 0;

    // Nome amigável dos domínios e roles do catálogo; o que não estiver aqui aparece com o nome técnico.
    private static readonly Dictionary<string, string> PolicyDomainNames = new(StringComparer.Ordinal)
    {
        ["Collections"] = "Collections",
        ["CompanyConfig"] = "Company settings",
        ["Diagrammer"] = "Diagrammer",
        ["DownloadHD"] = "HD download",
        ["Exporter"] = "Exporter",
        ["UsersConfig"] = "Users and permissions",
        ["IdManagement"] = "ID management",
        ["Invoices"] = "Invoices",
        ["PhotoTreatment"] = "Photo treatment",
        ["Products"] = "Products",
        ["ReferredClients"] = "Referred clients",
        ["Separator"] = "Separator",
        ["SearchGraduate"] = "Graduate search",
        ["Visualizer"] = "Visualizer",
    };

    private static readonly Dictionary<string, string> RoleNames = new(StringComparer.Ordinal)
    {
        ["Admin"] = "Administrator",
        ["Agent"] = "Agent",
        ["Separator"] = "Separator",
        ["user"] = "User",
        ["Photographer"] = "Photographer",
    };

    private static string FriendlyName(Dictionary<string, string> names, string key) =>
        names.TryGetValue(key, out var english) ? Loc.Tr(english) : key;

    private async Task LoadNewProfessionalPermissionOptionsAsync()
    {
        NewProfessionalPermissionsLoading = true;
        NewProfessionalPermissionsError = null;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.GetUserAuthorizationOptions();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (r?.success == true && r.Content != null)
                {
                    NewProfessionalRoles = new ObservableCollection<ProfessionalRoleOption>(
                        (r.Content.Roles ?? new List<string>()).Select(x => new ProfessionalRoleOption { Name = x, DisplayName = FriendlyName(RoleNames, x) }));
                    NewProfessionalPolicyDomains = new ObservableCollection<ProfessionalPolicyDomainRow>(
                        (r.Content.Policies ?? new List<PolicyDomain>())
                            .Select(d => ProfessionalPolicyDomainRow.From(d, FriendlyName(PolicyDomainNames, d.Domain)))
                            .OrderBy(d => d.DisplayName, StringComparer.CurrentCulture));
                }
                else
                {
                    NewProfessionalRoles = new();
                    NewProfessionalPolicyDomains = new();
                    NewProfessionalPermissionsError = Loc.Tr("Permissions are not available in this environment. The professional will be created without permissions.");
                }
                OnPropertyChanged(nameof(NewProfessionalPermissionsAvailable));
            });
        }
        catch (Exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                NewProfessionalRoles = new();
                NewProfessionalPolicyDomains = new();
                NewProfessionalPermissionsError = Loc.Tr("Permissions are not available in this environment. The professional will be created without permissions.");
                OnPropertyChanged(nameof(NewProfessionalPermissionsAvailable));
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => NewProfessionalPermissionsLoading = false);
        }
    }

    [RelayCommand]
    private void SelectAllNewProfessionalPolicies()
    {
        foreach (var d in NewProfessionalPolicyDomains)
            d.CanAll = true;
    }

    [RelayCommand]
    private void ClearNewProfessionalPermissions()
    {
        foreach (var d in NewProfessionalPolicyDomains)
            d.CanAll = false;
        foreach (var r in NewProfessionalRoles)
            r.IsChecked = false;
    }

    // null quando o catálogo não carregou: o cadastro segue sem permissões (e funciona também no legado).
    private UserAuthorizationConfig? BuildNewProfessionalAuthorization()
    {
        if (!NewProfessionalPermissionsAvailable)
            return null;
        return new UserAuthorizationConfig
        {
            Roles = NewProfessionalRoles.Where(r => r.IsChecked).Select(r => r.Name).ToHashSet(),
            Policies = NewProfessionalPolicyDomains.SelectMany(d => d.ToPolicies()).ToHashSet()
        };
    }
}
