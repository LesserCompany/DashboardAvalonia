using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// Quando a tela de instruções de atualização abre, em que modo, e se ela bloqueia o app.
[TestFixture]
public class UpdateInstructionsPolicyTests
{
    [Test]
    public void Store_com_update_obrigatorio_abre_a_tela_da_store_e_bloqueia()
    {
        var d = UpdateInstructionsPolicy.Decide(InstallSource.MicrosoftStore, StoreUpdateState.Mandatory);

        Assert.That(d.Mode, Is.EqualTo(UpdateInstructionsMode.StoreUpdate));
        Assert.That(d.IsBlocking, Is.True);
        Assert.That(d.CanPostpone, Is.False);
        Assert.That(d.ShouldShow, Is.True);
    }

    [Test]
    public void Store_com_update_opcional_abre_a_tela_da_store_com_mais_tarde()
    {
        var d = UpdateInstructionsPolicy.Decide(InstallSource.MicrosoftStore, StoreUpdateState.Optional);

        Assert.That(d.Mode, Is.EqualTo(UpdateInstructionsMode.StoreUpdate));
        Assert.That(d.IsBlocking, Is.False);
        Assert.That(d.CanPostpone, Is.True);
    }

    [TestCase(StoreUpdateState.None)]
    [TestCase(StoreUpdateState.Error)]
    public void Store_sem_update_ou_com_erro_na_checagem_nao_abre_nada(StoreUpdateState state)
    {
        var d = UpdateInstructionsPolicy.Decide(InstallSource.MicrosoftStore, state);

        Assert.That(d.Mode, Is.EqualTo(UpdateInstructionsMode.None));
        Assert.That(d.ShouldShow, Is.False);
        Assert.That(d.IsBlocking, Is.False);
    }

    // Instalador web antigo (Azure): a tela abre sempre, para migrar ao letspic.zip antes de o Azure ser desligado.
    [TestCase(StoreUpdateState.None)]
    [TestCase(StoreUpdateState.Optional)]
    [TestCase(StoreUpdateState.Mandatory)]
    [TestCase(StoreUpdateState.Error)]
    public void Web_antigo_sempre_abre_a_tela_de_reinstalar_pelo_zip_com_mais_tarde(StoreUpdateState state)
    {
        var d = UpdateInstructionsPolicy.Decide(InstallSource.LegacyWeb, state);

        Assert.That(d.Mode, Is.EqualTo(UpdateInstructionsMode.ReinstallFromZip));
        Assert.That(d.ShouldShow, Is.True);
        Assert.That(d.IsBlocking, Is.False);
        Assert.That(d.CanPostpone, Is.True);
    }

    // Quem já instalou pelo zip (MSIX fora da Store) já está na distribuição nova.
    [TestCase(StoreUpdateState.None)]
    [TestCase(StoreUpdateState.Mandatory)]
    public void Pacote_instalado_pelo_zip_nao_abre_nada(StoreUpdateState state)
    {
        var d = UpdateInstructionsPolicy.Decide(InstallSource.PackagedOutsideStore, state);

        Assert.That(d.ShouldShow, Is.False);
    }

    [Test]
    public void Origem_desconhecida_nao_abre_nada()
    {
        Assert.That(UpdateInstructionsPolicy.Decide(InstallSource.Unknown, StoreUpdateState.None).ShouldShow, Is.False);
    }

    [Test]
    public void Pacote_assinado_pela_store_vem_da_store()
    {
        var s = InstallSourceResolver.Resolve(isPackaged: true, isStoreSigned: true, @"C:\Program Files\WindowsApps\x\Apps\LesserDashboardClient\", @"C:\Users\Ana\Documents\Separacao\apps\");
        Assert.That(s, Is.EqualTo(InstallSource.MicrosoftStore));
    }

    [Test]
    public void Pacote_sem_assinatura_da_store_veio_do_zip()
    {
        var s = InstallSourceResolver.Resolve(isPackaged: true, isStoreSigned: false, @"C:\Program Files\WindowsApps\x\", @"C:\Users\Ana\Documents\Separacao\apps\");
        Assert.That(s, Is.EqualTo(InstallSource.PackagedOutsideStore));
    }

    [TestCase(@"C:\Users\Ana\Documents\Separacao\apps\LesserDashboard\v\188\", @"C:\Users\Ana\Documents/Separacao/apps/")]
    [TestCase(@"c:\users\ana\documents\separacao\apps\LesserDashboard\v\188", @"C:\Users\Ana\Documents\Separacao\apps")]
    public void Fora_de_pacote_rodando_da_pasta_do_instalador_web_e_web_antigo(string baseDir, string appsRoot)
    {
        Assert.That(InstallSourceResolver.Resolve(false, false, baseDir, appsRoot), Is.EqualTo(InstallSource.LegacyWeb));
    }

    [TestCase(@"C:\LesserSL\ClientSide\Dashboards\DashboardAvalonia\bin\Debug\net7.0-windows10.0.19041.0\")]
    [TestCase(@"C:\Users\Ana\Documents\Separacao\appsX\LesserDashboard\")]
    [TestCase("")]
    public void Fora_de_pacote_e_fora_da_pasta_do_instalador_web_e_desconhecido(string baseDir)
    {
        Assert.That(InstallSourceResolver.Resolve(false, false, baseDir, @"C:\Users\Ana\Documents\Separacao\apps\"), Is.EqualTo(InstallSource.Unknown));
    }

    // Aberto pela landing, que já mostrou o aviso opcional: o Dashboard não repete.
    [Test]
    public void Aberto_pela_landing_nao_repete_o_aviso_opcional()
    {
        var d = UpdateInstructionsPolicy.Decide(InstallSource.MicrosoftStore, StoreUpdateState.Optional, checkedByLanding: true);

        Assert.That(d.ShouldShow, Is.False);
    }

    [Test]
    public void Aberto_pela_landing_ainda_bloqueia_o_obrigatorio()
    {
        var d = UpdateInstructionsPolicy.Decide(InstallSource.MicrosoftStore, StoreUpdateState.Mandatory, checkedByLanding: true);

        Assert.That(d.IsBlocking, Is.True);
    }

    [TestCase(new[] { "--store-update-checked" }, true)]
    [TestCase(new[] { "autostart", "--STORE-UPDATE-CHECKED" }, true)]
    [TestCase(new[] { "autostart" }, false)]
    [TestCase(new string[0], false)]
    public void Argumento_da_landing_indica_que_ja_checou(string[] args, bool expected)
    {
        Assert.That(UpdateInstructionsPolicy.WasCheckedByLanding(args), Is.EqualTo(expected));
    }

    [TestCase("store-required", UpdateInstructionsMode.StoreUpdate, true)]
    [TestCase(" STORE-OPTIONAL ", UpdateInstructionsMode.StoreUpdate, false)]
    [TestCase("zip", UpdateInstructionsMode.ReinstallFromZip, false)]
    public void Demo_pela_variavel_de_ambiente_forca_o_modo(string value, UpdateInstructionsMode mode, bool blocking)
    {
        var d = UpdateInstructionsPolicy.TryParseDemo(value);

        Assert.That(d, Is.Not.Null);
        Assert.That(d!.Mode, Is.EqualTo(mode));
        Assert.That(d.IsBlocking, Is.EqualTo(blocking));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("qualquer")]
    public void Sem_demo_valida_segue_a_regra_normal(string? value)
    {
        Assert.That(UpdateInstructionsPolicy.TryParseDemo(value), Is.Null);
    }

    [TestCase(0, false, StoreUpdateState.None)]
    [TestCase(0, true, StoreUpdateState.None)]
    [TestCase(1, false, StoreUpdateState.Optional)]
    [TestCase(3, false, StoreUpdateState.Optional)]
    [TestCase(1, true, StoreUpdateState.Mandatory)]
    public void Estado_da_store_vem_da_contagem_de_updates_e_do_mandatory(int count, bool anyMandatory, StoreUpdateState expected)
    {
        Assert.That(UpdateInstructionsPolicy.StoreStateFrom(count, anyMandatory), Is.EqualTo(expected));
    }
}

[TestFixture]
public class UpdateLinksTests
{
    [Test]
    public void Zip_aponta_para_o_site_do_letspic()
    {
        Assert.That(UpdateLinks.ZipDownloadUrl, Is.EqualTo("https://letspic.com.br/letspic.zip"));
    }

    [Test]
    public void Pagina_da_store_usa_o_product_id_do_letspic()
    {
        Assert.That(UpdateLinks.StoreProductId, Is.EqualTo("9P5GDKBRXR16"));
        Assert.That(UpdateLinks.StoreProductPageUri, Is.EqualTo("ms-windows-store://pdp/?ProductId=9P5GDKBRXR16"));
    }

    [Test]
    public void Aplicativos_instalados_abre_as_configuracoes_do_windows()
    {
        Assert.That(UpdateLinks.InstalledAppsSettingsUri, Is.EqualTo("ms-settings:appsfeatures"));
    }
}
