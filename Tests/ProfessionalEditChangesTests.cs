using LesserDashboardClient.Models;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// Campos da tela "Editar profissional" -> o que vai para UpdateCompanyProfessional_v2.
[TestFixture]
public class ProfessionalEditChangesTests
{
    [Test]
    public void Sem_mudanca_nao_manda_nada()
    {
        var c = ProfessionalEditChanges.Compute("João", "joao@x.com", " João ", "JOAO@x.com", "", "");
        Assert.That(c.Error, Is.Null);
        Assert.That(c.HasFieldChanges, Is.False);
    }

    [Test]
    public void Manda_so_o_que_mudou()
    {
        var c = ProfessionalEditChanges.Compute("João", "joao@x.com", "João Silva", "joao@x.com", null, null);
        Assert.That(c.SurName, Is.EqualTo("João Silva"));
        Assert.That(c.ContactMail, Is.Null);
        Assert.That(c.NewPassword, Is.Null);
    }

    [Test]
    public void Apagar_o_apelido_manda_vazio()
    {
        var c = ProfessionalEditChanges.Compute("João", "joao@x.com", "  ", "joao@x.com", null, null);
        Assert.That(c.SurName, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Email_novo_vai_em_minusculas_e_invalido_e_recusado()
    {
        Assert.That(ProfessionalEditChanges.Compute(null, "a@x.com", null, " Novo@Y.com ", null, null).ContactMail, Is.EqualTo("novo@y.com"));
        Assert.That(ProfessionalEditChanges.Compute(null, "a@x.com", null, "sem-arroba", null, null).Error, Is.Not.Null);
    }

    [Test]
    public void Senha_precisa_de_tamanho_e_confirmacao()
    {
        Assert.That(ProfessionalEditChanges.Compute(null, null, null, null, "12345", "12345").Error, Is.Not.Null, "curta");
        Assert.That(ProfessionalEditChanges.Compute(null, null, null, null, "123456", "123457").Error, Is.Not.Null, "não confere");
        Assert.That(ProfessionalEditChanges.Compute(null, null, null, null, "", "123456").Error, Is.Not.Null, "só a confirmação");

        var ok = ProfessionalEditChanges.Compute(null, null, null, null, "123456", "123456");
        Assert.That(ok.Error, Is.Null);
        Assert.That(ok.NewPassword, Is.EqualTo("123456"));
    }
}
