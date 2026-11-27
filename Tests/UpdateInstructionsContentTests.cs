using System.Linq;
using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// Textos e passos da tela de instruções de atualização, por modo.
[TestFixture]
public class UpdateInstructionsContentTests
{
    private static readonly UpdateInstructionsDecision ZipMode = UpdateInstructionsPolicy.Decide(InstallSource.LegacyWeb, StoreUpdateState.None);
    private static readonly UpdateInstructionsDecision StoreRequired = UpdateInstructionsPolicy.Decide(InstallSource.MicrosoftStore, StoreUpdateState.Mandatory);
    private static readonly UpdateInstructionsDecision StoreOptional = UpdateInstructionsPolicy.Decide(InstallSource.MicrosoftStore, StoreUpdateState.Optional);

    // Desinstalar fecha o app e some esta tela com o link: baixar tem de vir antes.
    [Test]
    public void Modo_zip_baixa_antes_de_desinstalar()
    {
        var actions = UpdateInstructionsContentFactory.For(ZipMode).Steps.Select(s => s.Action).ToList();

        Assert.That(actions, Is.EqualTo(new[] { UpdateStepAction.DownloadZip, UpdateStepAction.OpenInstalledApps, UpdateStepAction.None }));
    }

    [Test]
    public void Modo_zip_tem_o_download_como_acao_principal()
    {
        var steps = UpdateInstructionsContentFactory.For(ZipMode).Steps;

        Assert.That(steps.Single(s => s.IsPrimaryAction).Action, Is.EqualTo(UpdateStepAction.DownloadZip));
    }

    [Test]
    public void Reinstalar_pela_store_abre_a_store_antes_de_desinstalar()
    {
        var actions = UpdateInstructionsContentFactory.For(StoreRequired).Steps.Select(s => s.Action).ToList();

        Assert.That(actions, Is.EqualTo(new[] { UpdateStepAction.OpenStore, UpdateStepAction.OpenInstalledApps, UpdateStepAction.None }));
    }

    [Test]
    public void Passos_sao_numerados_a_partir_de_1()
    {
        var numbers = UpdateInstructionsContentFactory.For(ZipMode).Steps.Select(s => s.Number);

        Assert.That(numbers, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void Tela_bloqueante_avisa_que_fechar_encerra_o_app()
    {
        var c = UpdateInstructionsContentFactory.For(StoreRequired);

        Assert.That(c.DismissLabel, Is.EqualTo("Fechar"));
        Assert.That(c.DismissNote, Does.Contain("fecha o aplicativo"));
    }

    [Test]
    public void Tela_nao_bloqueante_oferece_mais_tarde()
    {
        Assert.That(UpdateInstructionsContentFactory.For(StoreOptional).DismissLabel, Is.EqualTo("Mais tarde"));
        Assert.That(UpdateInstructionsContentFactory.For(ZipMode).DismissLabel, Is.EqualTo("Mais tarde"));
    }

    [Test]
    public void Obrigatorio_e_opcional_tem_titulos_diferentes()
    {
        Assert.That(UpdateInstructionsContentFactory.For(StoreRequired).Headline,
            Is.Not.EqualTo(UpdateInstructionsContentFactory.For(StoreOptional).Headline));
    }

    // Título curto: lido de relance, mesmo por quem fecharia sem ler.
    [Test]
    public void Titulos_tem_no_maximo_sete_palavras()
    {
        foreach (var d in new[] { ZipMode, StoreRequired, StoreOptional })
        {
            string headline = UpdateInstructionsContentFactory.For(d).Headline;
            Assert.That(headline.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length, Is.LessThanOrEqualTo(7), headline);
        }
    }
}
