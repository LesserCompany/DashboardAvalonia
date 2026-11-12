using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// Qual nota de atualização o dashboard pede ao servidor (GetAppReleaseNotes).
[TestFixture]
public class ReleaseNotesTargetTests
{
    [Test]
    public void Instalado_em_partes_pede_a_nota_do_dashboard_pela_pasta_da_versao()
    {
        var (app, version) = ReleaseNotesTarget.Resolve(false, null, " 188 ");
        Assert.That(app, Is.EqualTo("LesserDashboard"));
        Assert.That(version, Is.EqualTo("188"));
    }

    [Test]
    public void Pacote_da_loja_pede_a_nota_geral_pela_versao_do_pacote()
    {
        var (app, version) = ReleaseNotesTarget.Resolve(true, "LetsPic.Lesser_1.0.42.0_x64__8wekyb3d8bbwe", "LesserDashboardClient");
        Assert.That(app, Is.EqualTo("MicrosoftStore"));
        Assert.That(version, Is.EqualTo("1.0.42.0"));
    }

    [TestCase("LetsPic.Lesser_1.0.42.0_x64__8wekyb3d8bbwe", "1.0.42.0")]
    [TestCase("LetsPic.Lesser_2.3.0.0_neutral_~_8wekyb3d8bbwe", "2.3.0.0")]
    [TestCase("semversao", null)]
    [TestCase("Nome_naoeversao_x64__pub", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void ParsePackageVersion(string? fullName, string? expected)
    {
        Assert.That(ReleaseNotesTarget.ParsePackageVersion(fullName), Is.EqualTo(expected));
    }

    [Test]
    public void Pacote_sem_nome_legivel_manda_versao_vazia()
    {
        var (app, version) = ReleaseNotesTarget.Resolve(true, null, "188");
        Assert.That(app, Is.EqualTo("MicrosoftStore"));
        Assert.That(version, Is.EqualTo(""));
    }
}
