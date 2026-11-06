using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// Login do professional criado pelo dashboard: "{company}@{nome escolhido}" (ex.: euphoria@joao).
[TestFixture]
public class ProfessionalUsernamePrefixTests
{
    [Test]
    public void Prefixo_e_o_nome_da_company_com_arroba()
    {
        Assert.That(ProfessionalUsernamePrefix.FromCompany("euphoria"), Is.EqualTo("euphoria@"));
        Assert.That(ProfessionalUsernamePrefix.FromCompany("  Claudio Denner "), Is.EqualTo("ClaudioDenner@"), "espaço não é aceito no login");
        Assert.That(ProfessionalUsernamePrefix.FromCompany("ação@x"), Is.EqualTo("aox@"), "acento e @ saem do prefixo");
        Assert.That(ProfessionalUsernamePrefix.FromCompany("   "), Is.Empty);
        Assert.That(ProfessionalUsernamePrefix.FromCompany(null), Is.Empty);
    }

    [Test]
    public void Login_final_junta_prefixo_e_nome_sem_repetir()
    {
        Assert.That(ProfessionalUsernamePrefix.Build("euphoria@", " joao.silva "), Is.EqualTo("euphoria@joao.silva"));
        Assert.That(ProfessionalUsernamePrefix.Build("euphoria@", "Euphoria@joao"), Is.EqualTo("euphoria@joao"), "prefixo já digitado não duplica");
        Assert.That(ProfessionalUsernamePrefix.Build("euphoria@", ""), Is.Empty);
        Assert.That(ProfessionalUsernamePrefix.Build("", "joao"), Is.EqualTo("joao"));
    }

    [Test]
    public void Validacao_vale_para_a_parte_escolhida()
    {
        Assert.That(ProfessionalUsernamePrefix.IsValidTypedPart(ProfessionalUsernamePrefix.TypedPart("euphoria@", "joao"), 4), Is.True);
        Assert.That(ProfessionalUsernamePrefix.IsValidTypedPart(ProfessionalUsernamePrefix.TypedPart("euphoria@", "jo"), 4), Is.False, "curto");
        Assert.That(ProfessionalUsernamePrefix.IsValidTypedPart(ProfessionalUsernamePrefix.TypedPart("euphoria@", "jo@ao"), 4), Is.False, "@ só como separador");
        Assert.That(ProfessionalUsernamePrefix.IsValidTypedPart(ProfessionalUsernamePrefix.TypedPart("euphoria@", "joão"), 4), Is.False, "acento");
    }
}
