using System.Text.RegularExpressions;

namespace LesserDashboardClient.Models
{
    // Campos da tela "Editar profissional" -> o que mandar para UpdateCompanyProfessional_v2. Só vai o que mudou
    // (null = não mexe); senha só quando preenchida, e precisa bater com a confirmação.
    public sealed class ProfessionalEditChanges
    {
        // Mesmo mínimo de PgService.ProfessionalPasswordMinLength no servidor.
        public const int PasswordMinLength = 6;
        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        public string? Error { get; private init; }
        public string? SurName { get; private init; }
        public string? ContactMail { get; private init; }
        public string? NewPassword { get; private init; }

        public bool HasFieldChanges => SurName != null || ContactMail != null || NewPassword != null;

        public static ProfessionalEditChanges Compute(
            string? originalSurName, string? originalMail,
            string? editedSurName, string? editedMail,
            string? newPassword, string? confirmPassword)
        {
            var surName = (editedSurName ?? string.Empty).Trim();
            var mail = (editedMail ?? string.Empty).Trim().ToLowerInvariant();
            var origSurName = (originalSurName ?? string.Empty).Trim();
            var origMail = (originalMail ?? string.Empty).Trim().ToLowerInvariant();

            if (mail != origMail && !EmailPattern.IsMatch(mail))
                return new ProfessionalEditChanges { Error = "Email inválido." };

            string? password = null;
            if (!string.IsNullOrEmpty(newPassword) || !string.IsNullOrEmpty(confirmPassword))
            {
                if ((newPassword ?? string.Empty).Length < PasswordMinLength)
                    return new ProfessionalEditChanges { Error = $"A senha deve ter pelo menos {PasswordMinLength} caracteres." };
                if (newPassword != confirmPassword)
                    return new ProfessionalEditChanges { Error = "A confirmação da senha não confere." };
                password = newPassword;
            }

            return new ProfessionalEditChanges
            {
                // "" limpa o apelido no servidor.
                SurName = surName != origSurName ? surName : null,
                ContactMail = mail != origMail ? mail : null,
                NewPassword = password
            };
        }
    }
}
