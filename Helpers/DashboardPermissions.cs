using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient.Helpers;

/// <summary>
/// O que o usuário logado pode ver/fazer no dashboard. Company (e admin) vê tudo; professional vê
/// conforme as policies dele mais as liberadas pelas funções (UserAuthorization.PoliciesGrantedByRole)
/// (User.Authorization, que vem no login — o mesmo JSON de
/// UserAuthorizationConfig que o servidor usa). Mesma regra de AuthorizationMiddleware.HasPolicy no
/// servidor: "Domínio.AÇÃO" exato ou "Domínio.ALL".
///
/// É só para a interface: o servidor continua sendo a autoridade (endpoints com [Authenticate]).
/// </summary>
public sealed class DashboardPermissions
{
    private const string ProfessionalsUserType = "professionals";

    private readonly HashSet<string> _policies;
    private readonly HashSet<string> _roles;

    public bool IsCompany { get; }

    private DashboardPermissions(bool isCompany, HashSet<string> policies, HashSet<string>? roles = null)
    {
        IsCompany = isCompany;
        _policies = policies;
        _roles = roles ?? new HashSet<string>();
    }

    /// <summary>Funções do professional que aparecem na tela (sem as escondidas: Usuário, Agente).</summary>
    public IReadOnlyList<string> VisibleRoles =>
        _roles.Where(LesserDashboardClient.Models.ProfessionalRolePresets.IsVisibleRole).OrderBy(r => r, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Áreas com alguma ação liberada (diretas + pelas funções), com as ações GET/POST/PUT/DELETE de cada uma —
    /// base do resumo de permissões no cabeçalho. Company: lista vazia (não tem resumo, vê tudo).
    /// </summary>
    public IReadOnlyList<(string Domain, IReadOnlyList<string> Actions)> AreasWithActions()
    {
        if (IsCompany)
            return Array.Empty<(string, IReadOnlyList<string>)>();
        var actions = new[] { "GET", "POST", "PUT", "DELETE" };
        return UserAuthorization.GetAllPolicyDomains()
            .Select(d => (d.Domain, (IReadOnlyList<string>)actions.Where(a => Can(d.Domain, a)).ToList()))
            .Where(x => x.Item2.Count > 0)
            .ToList();
    }

    public static DashboardPermissions From(string? userType, string? authorizationJson)
    {
        if (!string.Equals(userType, ProfessionalsUserType, StringComparison.OrdinalIgnoreCase))
            return new DashboardPermissions(true, new HashSet<string>());

        UserAuthorizationConfig? auth = null;
        if (!string.IsNullOrWhiteSpace(authorizationJson))
        {
            try
            {
                auth = JsonConvert.DeserializeObject<UserAuthorizationConfig>(authorizationJson);
            }
            catch (JsonException)
            {
                // JSON inválido = sem permissões (nunca libera por engano).
            }
        }

        // Policies diretas + as liberadas pelas funções (ex.: Separador libera Coleções), igual ao servidor.
        auth ??= new UserAuthorizationConfig();
        return new DashboardPermissions(false, UserAuthorization.GetEffectivePolicies(auth),
            new HashSet<string>(auth.Roles ?? new HashSet<string>(), StringComparer.Ordinal));
    }

    /// <summary>true se pode a ação no domínio. Ex.: Can("Collections", "GET").</summary>
    public bool Can(string domain, string action) =>
        IsCompany || _policies.Contains($"{domain}.{action}") || _policies.Contains($"{domain}.ALL");

    // Abas do topo. "Options" (preferências locais do app) é sempre visível.
    public bool CanSeeCollections => Can("Collections", "GET");
    public bool CanSeeDiagrammer => Can("Diagrammer", "GET");
    public bool CanSeeSearchGraduate => Can("SearchGraduate", "GET");
    public bool CanSeeInvoices => Can("Invoices", "GET");
    public bool CanSeeCompanyConfig => Can("CompanyConfig", "GET");

    // Ações dentro da aba Coleções.
    public bool CanCreateCollection => Can("Collections", "POST");
    public bool CanManageProfessionals => Can("UsersConfig", "GET");

    /// <summary>Professional sem nenhuma aba de conteúdo liberada (só "Opções").</summary>
    public bool HasNoContentTabs =>
        !CanSeeCollections && !CanSeeDiagrammer && !CanSeeSearchGraduate && !CanSeeInvoices && !CanSeeCompanyConfig;
}
