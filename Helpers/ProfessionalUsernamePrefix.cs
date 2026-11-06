using System;
using System.Linq;

namespace LesserDashboardClient.Helpers
{
    // Login do professional criado pelo dashboard = "{company}@{o que foi digitado}" (ex.: euphoria@joao), para
    // ficar claro de qual empresa ele é e não colidir com logins de outras empresas. O servidor
    // (RegisterProfessional_v2) aceita no máximo um '@', só entre as duas partes.
    public static class ProfessionalUsernamePrefix
    {
        public const string Separator = "@";

        // Caracteres aceitos em cada parte do username: letras, números, '.', '-', '_'.
        public static bool IsAllowed(char c) => char.IsAsciiLetterOrDigit(c) || c == '.' || c == '-' || c == '_';

        // Prefixo a partir do username da company, sem os caracteres que o username não aceita. Vazio se não sobrar nada.
        public static string FromCompany(string? companyUsername)
        {
            var clean = new string((companyUsername ?? string.Empty).Trim().Where(IsAllowed).ToArray());
            return clean.Length == 0 ? string.Empty : clean + Separator;
        }

        // Junta prefixo e o digitado. Se a pessoa já digitou o prefixo, não repete.
        public static string Build(string prefix, string? typed)
        {
            var t = TypedPart(prefix, typed);
            return t.Length == 0 ? string.Empty : prefix + t;
        }

        // Parte escolhida pela pessoa, sem o prefixo (é ela que passa pela validação de tamanho e caracteres).
        public static string TypedPart(string prefix, string? typed)
        {
            var t = (typed ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(prefix) && t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                t = t.Substring(prefix.Length);
            return t;
        }

        public static bool IsValidTypedPart(string typedPart, int minLength) =>
            typedPart.Length >= minLength && typedPart.All(IsAllowed);
    }
}
