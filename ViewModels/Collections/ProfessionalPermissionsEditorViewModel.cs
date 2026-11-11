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

// Grade de permissões (roles + policies por área) do componente Views/Collections/ProfessionalPermissionsEditor.
// Usada no cadastro de profissional e na edição das permissões de um profissional existente. O catálogo vem do
// servidor (GetUserAuthorizationOptions_v2).
public partial class ProfessionalPermissionsEditorViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    private ObservableCollection<ProfessionalRoleOption> roles = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    private ObservableCollection<ProfessionalPolicyDomainRow> policyDomains = new();

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? loadError;
    [ObservableProperty] private bool isEnabled = true;

    /// <summary>Expander "Avançado" (grade de áreas) aberto.</summary>
    [ObservableProperty] private bool isAreasExpanded;

    public bool IsAvailable => Roles.Count > 0 || PolicyDomains.Count > 0;

    // Nome amigável das áreas e roles do catálogo; o que não estiver aqui aparece com o nome técnico.
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

    // Carrega o catálogo e marca o que `current` já tem (null = nada marcado, cadastro novo).
    public async Task LoadAsync(UserAuthorizationConfig? current)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            LoadError = null;
            Roles = new();
            PolicyDomains = new();
        });
        try
        {
            var r = await GlobalAppStateViewModel.lfc.GetUserAuthorizationOptions();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (r?.success != true || r.Content == null)
                {
                    LoadError = Loc.Tr("Permissions are not available in this environment.");
                    return;
                }

                var roles = current?.Roles ?? new HashSet<string>();
                var policies = current?.Policies ?? new HashSet<string>();

                Roles = new ObservableCollection<ProfessionalRoleOption>((r.Content.Roles ?? new List<string>())
                    .Select(x => new ProfessionalRoleOption { Name = x, DisplayName = FriendlyName(RoleNames, x), IsChecked = roles.Contains(x) }));

                var rows = (r.Content.Policies ?? new List<PolicyDomain>())
                    .Select(d => ProfessionalPolicyDomainRow.From(d, FriendlyName(PolicyDomainNames, d.Domain)))
                    .OrderBy(d => d.DisplayName, StringComparer.CurrentCulture)
                    .ToList();
                foreach (var row in rows)
                    row.LoadFrom(policies);
                PolicyDomains = new ObservableCollection<ProfessionalPolicyDomainRow>(rows);

                // Áreas ficam no "Avançado", fechado; abre sozinho se o professional já tem alguma área
                // marcada, para a edição não esconder permissões existentes.
                IsAreasExpanded = rows.Any(x => x.AnyChecked);
            });
        }
        catch (Exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() => LoadError = Loc.Tr("Permissions are not available in this environment."));
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var d in PolicyDomains)
            d.CanAll = true;
        // Marca áreas, que ficam no "Avançado": abre para a mudança não ficar escondida.
        IsAreasExpanded = true;
    }

    [RelayCommand]
    private void Clear()
    {
        foreach (var d in PolicyDomains)
            d.CanAll = false;
        foreach (var r in Roles)
            r.IsChecked = false;
    }

    // Permissões marcadas; null quando o catálogo não carregou.
    public UserAuthorizationConfig? Build()
    {
        if (!IsAvailable)
            return null;
        return new UserAuthorizationConfig
        {
            Roles = Roles.Where(r => r.IsChecked).Select(r => r.Name).ToHashSet(),
            Policies = PolicyDomains.SelectMany(d => d.ToPolicies()).ToHashSet()
        };
    }

    public IEnumerable<string> CatalogRoles => Roles.Select(r => r.Name);
    public IEnumerable<string> CatalogPolicies => PolicyDomains.SelectMany(d => d.RepresentablePolicies());
}
