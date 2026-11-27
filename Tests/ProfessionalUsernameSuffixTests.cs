using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// Login do professional criado pelo dashboard: "{nome escolhido}@{company}" (ex.: joao@euphoria), igual ao dashboard Svelte.
[TestFixture]
public class ProfessionalUsernameSuffixTests
{
    [Test]
    public void Sufixo_e_arroba_mais_o_nome_da_company()
    {
        Assert.That(ProfessionalUsernameSuffix.FromCompany("euphoria"), Is.EqualTo("@euphoria"));
        Assert.That(ProfessionalUsernameSuffix.FromCompany("  Claudio Denner "), Is.EqualTo("@ClaudioDenner"), "espaço não é aceito no login");
        Assert.That(ProfessionalUsernameSuffix.FromCompany("ação@x"), Is.EqualTo("@aox"), "acento e @ saem do sufixo");
        Assert.That(ProfessionalUsernameSuffix.FromCompany("   "), Is.Empty);
        Assert.That(ProfessionalUsernameSuffix.FromCompany(null), Is.Empty);
    }

    [Test]
    public void Login_final_junta_nome_e_sufixo_sem_repetir()
    {
        Assert.That(ProfessionalUsernameSuffix.Build("@teste", "Luiz"), Is.EqualTo("Luiz@teste"));
        Assert.That(ProfessionalUsernameSuffix.Build("@euphoria", " joao.silva "), Is.EqualTo("joao.silva@euphoria"));
        Assert.That(ProfessionalUsernameSuffix.Build("@euphoria", "joao@Euphoria"), Is.EqualTo("joao@euphoria"), "sufixo já digitado não duplica");
        Assert.That(ProfessionalUsernameSuffix.Build("@euphoria", ""), Is.Empty);
        Assert.That(ProfessionalUsernameSuffix.Build("", "joao"), Is.EqualTo("joao"));
    }

    [Test]
    public void Validacao_vale_para_a_parte_escolhida()
    {
        Assert.That(ProfessionalUsernameSuffix.IsValidTypedPart(ProfessionalUsernameSuffix.TypedPart("@euphoria", "joao"), 4), Is.True);
        Assert.That(ProfessionalUsernameSuffix.IsValidTypedPart(ProfessionalUsernameSuffix.TypedPart("@euphoria", "joao@euphoria"), 4), Is.True);
        Assert.That(ProfessionalUsernameSuffix.IsValidTypedPart(ProfessionalUsernameSuffix.TypedPart("@euphoria", "jo"), 4), Is.False, "curto");
        Assert.That(ProfessionalUsernameSuffix.IsValidTypedPart(ProfessionalUsernameSuffix.TypedPart("@euphoria", "jo@ao"), 4), Is.False, "@ só como separador");
        Assert.That(ProfessionalUsernameSuffix.IsValidTypedPart(ProfessionalUsernameSuffix.TypedPart("@euphoria", "joão"), 4), Is.False, "acento");
        Assert.That(ProfessionalUsernameSuffix.IsValidTypedPart(ProfessionalUsernameSuffix.TypedPart("@euphoria", "Luiz@teste"), 4), Is.False, "não dá para escolher outra company");
    }
}
