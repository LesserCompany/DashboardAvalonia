using System;
using System.Linq;

namespace LesserDashboardClient.Helpers
{
    // Login do professional criado pelo dashboard = "{o que foi digitado}@{company}" (ex.: joao@euphoria), igual ao
    // dashboard Svelte, para ficar claro de qual empresa ele é e não colidir com logins de outras empresas. O servidor
    // (RegisterProfessional_v2) aceita no máximo um '@', só entre as duas partes; por isso '@' digitado pela pessoa
    // (ex.: "Luiz@teste" numa company "euphoria") é recusado, a não ser que seja o próprio sufixo da company.
    public static class ProfessionalUsernameSuffix
    {
        public const string Separator = "@";

        // Caracteres aceitos em cada parte do username: letras, números, '.', '-', '_'.
        public static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c == '.' || c == '-' || c == '_';

        // Sufixo a partir do username da company, sem os caracteres que o username não aceita. Vazio se não sobrar nada.
        public static string FromCompany(string? companyUsername)
        {
            var clean = new string((companyUsername ?? string.Empty).Trim().Where(IsAllowed).ToArray());
            return clean.Length == 0 ? string.Empty : Separator + clean;
        }

        // Junta o digitado e o sufixo. Se a pessoa já digitou o sufixo, não repete.
        public static string Build(string suffix, string? typed)
        {
            var t = TypedPart(suffix, typed);
            return t.Length == 0 ? string.Empty : t + suffix;
        }

        // Parte escolhida pela pessoa, sem o sufixo (é ela que passa pela validação de tamanho e caracteres).
        public static string TypedPart(string suffix, string? typed)
        {
            var t = (typed ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(suffix) && t.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                t = t.Substring(0, t.Length - suffix.Length);
            return t;
        }

        public static bool IsValidTypedPart(string typedPart, int minLength) =>
            typedPart.Length >= minLength && typedPart.All(IsAllowed);
    }
}
