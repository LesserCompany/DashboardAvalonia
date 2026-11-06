using Newtonsoft.Json;
using NUnit.Framework;
using SharedClientSide.ServerInteraction;

namespace LesserDashboardClient.Tests;

// Label da coleção (ProfessionalTask.Label / DisplayName / HasLabel): o nome mostrado é a label, ou o classCode.
[TestFixture]
public class ProfessionalTaskLabelTests
{
    [Test]
    public void Sem_label_mostra_o_classCode()
    {
        var pt = new ProfessionalTask { classCode = "UNINOVE612FG" };
        Assert.That(pt.DisplayName, Is.EqualTo("UNINOVE612FG"));
        Assert.That(pt.HasLabel, Is.False);

        pt.Label = "   ";
        Assert.That(pt.DisplayName, Is.EqualTo("UNINOVE612FG"));
        Assert.That(pt.HasLabel, Is.False);
    }

    [Test]
    public void Com_label_mostra_a_label_e_avisa_a_tela()
    {
        var pt = new ProfessionalTask { classCode = "UNINOVE612FG" };
        var avisos = new List<string?>();
        pt.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        pt.Label = "Medicina Uninove 2026";

        Assert.That(pt.DisplayName, Is.EqualTo("Medicina Uninove 2026"));
        Assert.That(pt.HasLabel, Is.True);
        Assert.That(avisos, Does.Contain(nameof(ProfessionalTask.DisplayName)));
        Assert.That(avisos, Does.Contain(nameof(ProfessionalTask.HasLabel)));
    }

    [Test]
    public void Label_vem_do_servidor_e_DisplayName_nao_vai_no_json()
    {
        var pt = JsonConvert.DeserializeObject<ProfessionalTask>("{\"classCode\":\"ABC\",\"Label\":\"Nome\"}")!;
        Assert.That(pt.DisplayName, Is.EqualTo("Nome"));

        var json = JsonConvert.SerializeObject(pt);
        Assert.That(json, Does.Not.Contain("DisplayName"));
        Assert.That(json, Does.Not.Contain("HasLabel"));
    }
}
