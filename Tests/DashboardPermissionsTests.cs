using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// O que cada tipo de login vê no dashboard (abas do topo e ações da aba Coleções).
[TestFixture]
public class DashboardPermissionsTests
{
    [Test]
    public void Company_ve_tudo_mesmo_sem_policies()
    {
        var p = DashboardPermissions.From("companies", null);
        Assert.That(p.IsCompany, Is.True);
        Assert.That(p.CanSeeCollections && p.CanSeeDiagrammer && p.CanSeeSearchGraduate && p.CanSeeInvoices && p.CanSeeCompanyConfig, Is.True);
        Assert.That(p.CanCreateCollection && p.CanManageProfessionals, Is.True);
        Assert.That(p.HasNoContentTabs, Is.False);
    }

    [Test]
    public void Professional_ve_so_o_que_tem_policy()
    {
        var p = DashboardPermissions.From("professionals", "{\"Roles\":[],\"Policies\":[\"Collections.GET\",\"Invoices.ALL\"]}");
        Assert.That(p.IsCompany, Is.False);
        Assert.That(p.CanSeeCollections, Is.True);
        Assert.That(p.CanSeeInvoices, Is.True, "Domínio.ALL cobre GET");
        Assert.That(p.CanSeeDiagrammer, Is.False);
        Assert.That(p.CanSeeSearchGraduate, Is.False);
        Assert.That(p.CanSeeCompanyConfig, Is.False);
        Assert.That(p.CanCreateCollection, Is.False, "Collections.GET não dá POST");
        Assert.That(p.CanManageProfessionals, Is.False);
    }

    [Test]
    public void Professional_com_ALL_de_colecoes_pode_criar()
    {
        var p = DashboardPermissions.From("professionals", "{\"Policies\":[\"Collections.ALL\"]}");
        Assert.That(p.CanSeeCollections && p.CanCreateCollection, Is.True);
    }

    [Test]
    public void Professional_sem_permissoes_ou_json_invalido_nao_ve_nada()
    {
        foreach (var json in new[] { null, "", "{}", "nao-e-json", "{\"Policies\":null}" })
        {
            var p = DashboardPermissions.From("professionals", json);
            Assert.That(p.HasNoContentTabs, Is.True, json ?? "null");
            Assert.That(p.CanCreateCollection || p.CanManageProfessionals, Is.False, json ?? "null");
        }
    }
}
