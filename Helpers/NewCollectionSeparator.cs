namespace LesserDashboardClient.Helpers
{
    // Separador padrão de uma coleção nova (tela de nova coleção; ainda pode ser trocado pela lista de profissionais):
    //   - login de professional: ele mesmo (username completo do login, ex.: "test@testClaudio2" — é o mesmo valor
    //     gravado em professionalLogin);
    //   - login de company: o profissional selecionado na lista ou, sem nenhum, a própria company (comportamento antigo).
    public static class NewCollectionSeparator
    {
        public static string Default(bool isCompanyLogin, string? loggedUsername, string? selectedProfessionalUsername, string? companyUsername)
        {
            if (!isCompanyLogin && !string.IsNullOrWhiteSpace(loggedUsername))
                return loggedUsername.Trim();
            if (!string.IsNullOrWhiteSpace(selectedProfessionalUsername))
                return selectedProfessionalUsername;
            return companyUsername ?? string.Empty;
        }
    }
}
