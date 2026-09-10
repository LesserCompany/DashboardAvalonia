using System;

namespace LesserDashboardClient.Helpers;

/// <summary>
/// Intervalo da atualização periódica da lista de coleções (endpoint GetCompanyProfessionalTasks).
/// </summary>
public static class CollectionsListRefreshSettings
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
}
