using SharedClientSide.ServerInteraction.Users.Graduate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LesserDashboardClient.Helpers;

/// <summary>
/// Detecta conflitos de duplicidade na lista de formandos (ShortPath e CPF únicos por turma).
/// Usa HashSet/Dictionary com comparação case-insensitive, mesmo padrão de duplicados Eventos/Rec.
/// </summary>
public static class GraduateByCpfDuplicateChecker
{
    public enum ConflictKind
    {
        DuplicateShortPath,
        DuplicateCpf
    }

    public readonly struct Conflict
    {
        public ConflictKind Kind { get; init; }
        public string Key { get; init; }
        public string FirstShortPath { get; init; }
        public string FirstCpf { get; init; }
        public string SecondShortPath { get; init; }
        public string SecondCpf { get; init; }

        public string Describe()
        {
            var cpf1 = FormatCpf(FirstCpf);
            var cpf2 = FormatCpf(SecondCpf);
            return Kind switch
            {
                ConflictKind.DuplicateShortPath =>
                    $"• Foto \"{Key}\": CPF {cpf1} e CPF {cpf2}",
                ConflictKind.DuplicateCpf =>
                    $"• CPF {FormatCpf(Key)}: fotos \"{FirstShortPath}\" e \"{SecondShortPath}\"",
                _ => $"• {Key}"
            };
        }
    }

    public static string NormalizeCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return "";
        return new string(cpf.Where(char.IsDigit).ToArray());
    }

    public static string NormalizeShortPath(string? shortPath)
    {
        if (string.IsNullOrWhiteSpace(shortPath))
            return "";

        var normalized = shortPath.Trim().TrimStart('\\', '/').Replace("\\", "/");
        var fileName = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(fileName) ? normalized : fileName;
    }

    public static IReadOnlyList<Conflict> FindConflicts(IEnumerable<GraduateByCPF?>? graduates)
    {
        if (graduates == null)
            return Array.Empty<Conflict>();

        var conflicts = new List<Conflict>();
        var byShortPath = new Dictionary<string, (string shortPath, string cpf)>(StringComparer.OrdinalIgnoreCase);
        var byCpf = new Dictionary<string, (string shortPath, string cpf)>(StringComparer.OrdinalIgnoreCase);

        foreach (var g in graduates)
        {
            if (g == null)
                continue;

            var shortPathNorm = NormalizeShortPath(g.ShortPath);
            var cpfNorm = NormalizeCpf(g.CPF);
            var shortPathDisplay = string.IsNullOrWhiteSpace(g.ShortPath) ? shortPathNorm : g.ShortPath.Trim();
            var cpfDisplay = string.IsNullOrWhiteSpace(g.CPF) ? cpfNorm : g.CPF.Trim();

            if (!string.IsNullOrWhiteSpace(shortPathNorm))
            {
                if (byShortPath.TryGetValue(shortPathNorm, out var existing))
                {
                    conflicts.Add(new Conflict
                    {
                        Kind = ConflictKind.DuplicateShortPath,
                        Key = shortPathNorm,
                        FirstShortPath = existing.shortPath,
                        FirstCpf = existing.cpf,
                        SecondShortPath = shortPathDisplay,
                        SecondCpf = cpfDisplay
                    });
                }
                else
                {
                    byShortPath[shortPathNorm] = (shortPathDisplay, cpfDisplay);
                }
            }

            if (!string.IsNullOrWhiteSpace(cpfNorm))
            {
                if (byCpf.TryGetValue(cpfNorm, out var existing))
                {
                    if (!string.Equals(NormalizeShortPath(existing.shortPath), shortPathNorm, StringComparison.OrdinalIgnoreCase))
                    {
                        conflicts.Add(new Conflict
                        {
                            Kind = ConflictKind.DuplicateCpf,
                            Key = cpfNorm,
                            FirstShortPath = existing.shortPath,
                            FirstCpf = existing.cpf,
                            SecondShortPath = shortPathDisplay,
                            SecondCpf = cpfDisplay
                        });
                    }
                }
                else
                {
                    byCpf[cpfNorm] = (shortPathDisplay, cpfDisplay);
                }
            }
        }

        return conflicts;
    }

    public static string? ValidateOrGetMessage(IEnumerable<GraduateByCPF?>? graduates, int maxShow = 15)
    {
        var conflicts = FindConflicts(graduates);
        if (conflicts.Count == 0)
            return null;
        return BuildBlockingMessage(conflicts, maxShow);
    }

    public static string BuildBlockingMessage(IReadOnlyList<Conflict> conflicts, int maxShow = 15)
    {
        var intro =
            "Existem formandos duplicados na lista: a mesma foto de reconhecimento ou o mesmo CPF não pode aparecer mais de uma vez.\n\n" +
            "Corrija os conflitos abaixo e tente novamente.\n\n" +
            "— Conflitos —";

        var lines = conflicts.Take(maxShow).Select(c => c.Describe()).ToList();
        var more = conflicts.Count > maxShow ? conflicts.Count - maxShow : 0;
        if (more > 0)
            lines.Add($"... e mais {more} conflito(s).");

        return intro + "\n" + string.Join("\n", lines);
    }

    private static string FormatCpf(string? cpf)
    {
        var norm = NormalizeCpf(cpf);
        return string.IsNullOrWhiteSpace(norm) ? "(vazio)" : norm;
    }
}
