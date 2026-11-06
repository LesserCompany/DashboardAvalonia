using LesserDashboardClient.Models;
using NUnit.Framework;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient.Tests;

// Caixas de permissão do cadastro de professional -> policies "Domínio.AÇÃO" enviadas ao servidor.
[TestFixture]
public class ProfessionalPermissionOptionsTests
{
    private static ProfessionalPolicyDomainRow Linha(string domain) =>
        ProfessionalPolicyDomainRow.From(
            UserAuthorization.GetAllPolicyDomains().Single(d => d.Domain == domain), domain);

    [Test]
    public void Catalogo_do_cliente_tem_as_quatro_acoes_e_ALL_em_cada_dominio()
    {
        foreach (var d in UserAuthorization.GetAllPolicyDomains())
        {
            var r = ProfessionalPolicyDomainRow.From(d, d.Domain);
            Assert.That(r.HasView && r.HasCreate && r.HasEdit && r.HasDelete && r.HasAll, Is.True, d.Domain);
        }
    }

    [Test]
    public void Nada_marcado_nao_gera_policy()
    {
        Assert.That(Linha("Collections").ToPolicies(), Is.Empty);
    }

    [Test]
    public void Acoes_avulsas_viram_uma_policy_cada()
    {
        var r = Linha("Collections");
        r.CanView = true;
        r.CanEdit = true;
        Assert.That(r.ToPolicies(), Is.EquivalentTo(new[] { "Collections.GET", "Collections.PUT" }));
        Assert.That(r.CanAll, Is.False);
    }

    [Test]
    public void Todas_marcadas_vira_ALL()
    {
        var r = Linha("Invoices");
        r.CanView = r.CanCreate = r.CanEdit = r.CanDelete = true;
        Assert.That(r.CanAll, Is.True);
        Assert.That(r.ToPolicies(), Is.EquivalentTo(new[] { "Invoices.ALL" }));
    }

    [Test]
    public void Marcar_e_desmarcar_Tudo_mexe_em_todas_e_avisa_a_tela()
    {
        var r = Linha("Separator");
        var avisos = new List<string?>();
        r.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        r.CanAll = true;
        Assert.That(r.CanView && r.CanCreate && r.CanEdit && r.CanDelete, Is.True);
        Assert.That(avisos, Does.Contain(nameof(ProfessionalPolicyDomainRow.CanAll)));

        r.CanAll = false;
        Assert.That(r.AnyChecked, Is.False);
    }

    [Test]
    public void Desmarcar_uma_acao_tira_o_Tudo()
    {
        var r = Linha("Exporter");
        r.CanAll = true;
        r.CanDelete = false;
        Assert.That(r.CanAll, Is.False);
        Assert.That(r.ToPolicies(), Is.EquivalentTo(new[] { "Exporter.GET", "Exporter.POST", "Exporter.PUT" }));
    }
}
