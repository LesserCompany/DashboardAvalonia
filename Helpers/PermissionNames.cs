using System;
using System.Collections.Generic;
using System.Linq;
using CodingSeb.Localization;

namespace LesserDashboardClient.Helpers;

/// <summary>
/// Nomes amigáveis (chave em inglês, traduzida por Loc.Tr) das áreas e funções do catálogo de permissões.
/// Usado no editor de permissões do profissional e no resumo do cabeçalho do dashboard.
/// O que não estiver aqui aparece com o nome técnico.
/// </summary>
public static class PermissionNames
{
    public static readonly Dictionary<string, string> PolicyDomains = new(StringComparer.Ordinal)
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

    public static readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal)
    {
        ["Admin"] = "Administrator",
        ["Agent"] = "Agent",
        ["Separator"] = "Separator",
        ["user"] = "User",
        ["Photographer"] = "Photographer",
    };

    private static readonly (string action, string name)[] Actions =
    {
        ("GET", "View"), ("POST", "Create"), ("PUT", "Edit"), ("DELETE", "Delete"),
    };

    public static string Friendly(Dictionary<string, string> names, string key) =>
        names.TryGetValue(key, out var english) ? Loc.Tr(english) : key;

    /// <summary>"Separador" / "Administrador, Fotógrafo"; "Personalizadas" sem função mas com área; "Nenhuma".</summary>
    public static string Summary(DashboardPermissions p)
    {
        var roles = p.VisibleRoles.Select(r => Friendly(Roles, r)).OrderBy(x => x, StringComparer.CurrentCulture).ToList();
        if (roles.Count > 0)
            return string.Join(", ", roles);
        return p.AreasWithActions().Count > 0 ? Loc.Tr("Custom") : Loc.Tr("None");
    }

    /// <summary>Uma linha por área liberada: "Coleções: Ver, Criar, Editar".</summary>
    public static string Detail(DashboardPermissions p)
    {
        var lines = p.AreasWithActions()
            .Select(a => (name: Friendly(PolicyDomains, a.Domain),
                          actions: string.Join(", ", Actions.Where(x => a.Actions.Contains(x.action)).Select(x => Loc.Tr(x.name)))))
            .OrderBy(x => x.name, StringComparer.CurrentCulture)
            .Select(x => $"{x.name}: {x.actions}")
            .ToList();
        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : Loc.Tr("None");
    }
}
