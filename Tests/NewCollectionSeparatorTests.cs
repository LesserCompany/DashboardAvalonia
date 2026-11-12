using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// Separador padrão da coleção nova.
[TestFixture]
public class NewCollectionSeparatorTests
{
    [Test]
    public void Professional_logado_e_o_proprio_separador_mesmo_com_outro_selecionado()
    {
        Assert.That(NewCollectionSeparator.Default(false, "test@testClaudio2", "test@outro", "test"), Is.EqualTo("test@testClaudio2"));
    }

    [Test]
    public void Company_usa_o_profissional_selecionado()
    {
        Assert.That(NewCollectionSeparator.Default(true, "test", "test@joao", "test"), Is.EqualTo("test@joao"));
    }

    [Test]
    public void Company_sem_profissionais_usa_a_propria_company()
    {
        Assert.That(NewCollectionSeparator.Default(true, "test", null, "test"), Is.EqualTo("test"));
    }

    [Test]
    public void Professional_sem_username_cai_na_regra_da_company()
    {
        Assert.That(NewCollectionSeparator.Default(false, "  ", "test@joao", "test"), Is.EqualTo("test@joao"));
    }
}
