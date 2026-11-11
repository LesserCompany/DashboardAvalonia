using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient.Helpers;

/// <summary>
/// O que o usuário logado pode ver/fazer no dashboard. Company (e admin) vê tudo; professional vê
/// conforme as policies dele (User.Authorization, que vem no login — o mesmo JSON de
/// UserAuthorizationConfig que o servidor usa). Mesma regra de AuthorizationMiddleware.HasPolicy no
/// servidor: "Domínio.AÇÃO" exato ou "Domínio.ALL".
///
/// É só para a interface: o servidor continua sendo a autoridade (endpoints com [Authenticate]).
/// </summary>
public sealed class DashboardPermissions
{
    private const string ProfessionalsUserType = "professionals";

    private readonly HashSet<string> _policies;

    public bool IsCompany { get; }

    private DashboardPermissions(bool isCompany, HashSet<string> policies)
    {
        IsCompany = isCompany;
        _policies = policies;
    }

    public static DashboardPermissions From(string? userType, string? authorizationJson)
    {
        if (!string.Equals(userType, ProfessionalsUserType, StringComparison.OrdinalIgnoreCase))
            return new DashboardPermissions(true, new HashSet<string>());

        HashSet<string>? policies = null;
        if (!string.IsNullOrWhiteSpace(authorizationJson))
        {
            try
            {
                policies = JsonConvert.DeserializeObject<UserAuthorizationConfig>(authorizationJson)?.Policies;
            }
            catch (JsonException)
            {
                // JSON inválido = sem permissões (nunca libera por engano).
            }
        }

        return new DashboardPermissions(false, new HashSet<string>(policies ?? new HashSet<string>(), StringComparer.Ordinal));
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
