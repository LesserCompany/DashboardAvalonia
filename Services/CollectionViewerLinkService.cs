using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedClientSide.ServerInteraction.Users.Companies;
using System;
using System.Collections.Generic;

namespace LesserDashboardClient.Services;

/// <summary>
/// Monta links do visualizador para compartilhamento de coleções (alinhado ao dash-svelte / personalizar).
/// </summary>
public static class CollectionViewerLinkService
{
    public const string DefaultViewerBaseUrl = "https://visualizador.lesser.biz";

    public static string ResolveCompanyUsername(Company company)
    {
        if (company == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(company.company))
            return company.company.Trim();

        return company.username?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Com domínio personalizado: {domain}/?classCode=...&amp;company=...
    /// Sem domínio: {DefaultViewerBaseUrl}/?company=...&amp;classCode=...
    /// </summary>
    public static string BuildCollectionShareLink(string classCode, string companyUsername, string? customProductionDomain)
    {
        if (string.IsNullOrWhiteSpace(classCode))
            throw new ArgumentException("classCode é obrigatório.", nameof(classCode));
        if (string.IsNullOrWhiteSpace(companyUsername))
            throw new ArgumentException("companyUsername é obrigatório.", nameof(companyUsername));

        var encodedClassCode = Uri.EscapeDataString(classCode);
        var encodedCompany = Uri.EscapeDataString(companyUsername);

        if (!string.IsNullOrWhiteSpace(customProductionDomain))
        {
            var baseUrl = NormalizeDomainUrl(customProductionDomain).TrimEnd('/');
            return $"{baseUrl}/?classCode={encodedClassCode}&company={encodedCompany}";
        }

        return $"{DefaultViewerBaseUrl}/?company={encodedCompany}&classCode={encodedClassCode}";
    }

    /// <summary>
    /// Lista bases para o seletor: visualizador padrão + cada domínio personalizado (sem duplicatas).
    /// </summary>
    public static IReadOnlyList<(string? CustomDomain, string DisplayHost)> EnumerateShareLinkBases(Company? company)
    {
        var list = new List<(string? CustomDomain, string DisplayHost)>
        {
            (null, GetDisplayHost(DefaultViewerBaseUrl))
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DefaultViewerBaseUrl };

        if (company?.domains == null)
            return list;

        foreach (var entry in company.domains)
        {
            var domain = GetDomainFromEntry(entry);
            if (string.IsNullOrWhiteSpace(domain))
                continue;

            var normalized = NormalizeDomainUrl(domain).TrimEnd('/');
            if (!seen.Add(normalized))
                continue;

            list.Add((domain, GetDisplayHost(normalized)));
        }

        return list;
    }

    public static string GetDisplayHost(string urlOrDomain)
    {
        var normalized = NormalizeDomainUrl(urlOrDomain);
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return uri.Host;

        return normalized
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
    }

    public static string NormalizeDomainUrl(string domain)
    {
        var trimmed = domain.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return $"https://{trimmed.TrimStart('/')}";
    }

    private static string? GetDomainFromEntry(CompanyDomainEntry entry)
    {
        if (entry == null)
            return null;

        if (!string.IsNullOrWhiteSpace(entry.domain))
            return entry.domain.Trim();

        if (entry.distributionWebsiteConfigString == null)
            return null;

        var json = entry.distributionWebsiteConfigString is string s
            ? s
            : JsonConvert.SerializeObject(entry.distributionWebsiteConfigString);

        return TryGetProductionDomainFromConfigJson(json);
    }

    private static string? TryGetProductionDomainFromConfigJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var obj = JObject.Parse(json);
            var prod = obj["productionConfig"] ?? obj["ProductionConfig"];
            var domain = prod?["domain"]?.ToString() ?? prod?["Domain"]?.ToString();
            return string.IsNullOrWhiteSpace(domain) ? null : domain.Trim();
        }
        catch
        {
            return null;
        }
    }
}
