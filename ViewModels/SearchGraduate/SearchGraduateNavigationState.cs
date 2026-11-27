using System.Collections.Generic;

namespace LesserDashboardClient.ViewModels.SearchGraduate;

/// <summary>
/// Estado de navegação entre Coleções → Busca por CPF (índice 0).
/// Os CPFs ficam aqui até serem injetados via ExecuteScriptAsync no WebView (fallback: query <c>filter</c>).
/// </summary>
public static class SearchGraduateNavigationState
{
    /// <summary>CPFs pendentes para injetar na Busca por CPF (somente dígitos).</summary>
    public static IReadOnlyList<string>? PendingCpfs { get; set; }

    public static bool HasPendingCpfs => PendingCpfs is { Count: > 0 };

    public static void Clear() => PendingCpfs = null;
}
