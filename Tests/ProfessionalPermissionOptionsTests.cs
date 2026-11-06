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
    public void Carregar_permissoes_atuais_marca_as_caixas()
    {
        var atuais = new HashSet<string> { "Collections.GET", "Collections.DELETE", "Invoices.ALL" };

        var colecoes = Linha("Collections");
        colecoes.LoadFrom(atuais);
        Assert.That(colecoes.CanView && colecoes.CanDelete, Is.True);
        Assert.That(colecoes.CanCreate || colecoes.CanEdit || colecoes.CanAll, Is.False);

        var faturas = Linha("Invoices");
        faturas.LoadFrom(atuais);
        Assert.That(faturas.CanAll, Is.True, "Domínio.ALL marca todas");
    }

    private static readonly string[] CatalogoRoles = UserAuthorization.GetAllRoles().ToArray();
    private static readonly string[] CatalogoPolicies = UserAuthorization.GetAllPolicyDomains().SelectMany(d => d.Actions).ToArray();

    [Test]
    public void Diferenca_adiciona_o_marcado_e_remove_o_desmarcado()
    {
        var atual = new UserAuthorizationConfig
        {
            Roles = new HashSet<string> { "Separator" },
            Policies = new HashSet<string> { "Collections.GET", "Invoices.ALL" }
        };
        var escolhido = new UserAuthorizationConfig
        {
            Roles = new HashSet<string> { "Photographer" },
            Policies = new HashSet<string> { "Collections.GET", "Invoices.GET" }
        };

        var d = ProfessionalPermissionsDiff.Compute(atual, escolhido, CatalogoRoles, CatalogoPolicies);

        Assert.That(d.RolesToAdd, Is.EqualTo(new[] { "Photographer" }));
        Assert.That(d.RolesToRemove, Is.EqualTo(new[] { "Separator" }));
        Assert.That(d.PoliciesToAdd, Is.EqualTo(new[] { "Invoices.GET" }));
        Assert.That(d.PoliciesToRemove, Is.EqualTo(new[] { "Invoices.ALL" }));
    }

    [Test]
    public void Diferenca_nao_remove_o_que_a_tela_nao_mostra()
    {
        var atual = new UserAuthorizationConfig
        {
            Roles = new HashSet<string> { "RoleAntiga" },
            Policies = new HashSet<string> { "Legado.GET", "Collections.GET" }
        };
        var escolhido = new UserAuthorizationConfig();

        var d = ProfessionalPermissionsDiff.Compute(atual, escolhido, CatalogoRoles, CatalogoPolicies);

        Assert.That(d.RolesToRemove, Is.Empty, "role fora do catálogo fica");
        Assert.That(d.PoliciesToRemove, Is.EqualTo(new[] { "Collections.GET" }), "policy fora do catálogo fica");
    }

    [Test]
    public void Sem_mudanca_a_diferenca_e_vazia()
    {
        var atual = new UserAuthorizationConfig { Policies = new HashSet<string> { "Collections.GET" } };
        var d = ProfessionalPermissionsDiff.Compute(atual, new UserAuthorizationConfig { Policies = new HashSet<string> { "Collections.GET" } }, CatalogoRoles, CatalogoPolicies);
        Assert.That(d.IsEmpty, Is.True);
        Assert.That(ProfessionalPermissionsDiff.Compute(null, new UserAuthorizationConfig(), CatalogoRoles, CatalogoPolicies).IsEmpty, Is.True);
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
