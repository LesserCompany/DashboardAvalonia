using System.Collections.Generic;
using System.Linq;
using LesserDashboardClient.Helpers;
using LesserDashboardClient.Models;
using NUnit.Framework;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient.Tests;

// O que cada função libera (UserAuthorization.PoliciesGrantedByRole, a mesma tabela que o servidor usa) e a
// marcação automática das áreas na tela de permissões do profissional.
[TestFixture]
public class ProfessionalRolePresetsTests
{
    private static List<ProfessionalPolicyDomainRow> Grade() =>
        UserAuthorization.GetAllPolicyDomains().Select(d => ProfessionalPolicyDomainRow.From(d, d.Domain)).ToList();

    private static ProfessionalPolicyDomainRow Linha(List<ProfessionalPolicyDomainRow> grade, string dominio) =>
        grade.Single(x => x.Domain == dominio);

    [Test]
    public void Separador_libera_ver_criar_e_editar_de_colecoes_separador_e_IDs_sem_excluir()
    {
        var liberadas = UserAuthorization.GetPoliciesGrantedByRoles(new[] { "Separator" });
        Assert.That(liberadas, Is.EquivalentTo(new[]
        {
            "Collections.GET", "Collections.POST", "Collections.PUT",
            "Separator.GET", "Separator.POST", "Separator.PUT",
            "IdManagement.GET", "IdManagement.POST", "IdManagement.PUT",
        }));
        Assert.That(UserAuthorization.GetPoliciesGrantedByRoles(new[] { "user", "Agent" }), Is.Empty, "funções sem tabela não liberam nada");
    }

    [Test]
    public void Policies_efetivas_juntam_as_diretas_e_as_da_funcao()
    {
        var auth = new UserAuthorizationConfig
        {
            Roles = new HashSet<string> { "Separator" },
            Policies = new HashSet<string> { "Invoices.GET" },
        };
        var efetivas = UserAuthorization.GetEffectivePolicies(auth);
        Assert.That(efetivas, Does.Contain("Invoices.GET"));
        Assert.That(efetivas, Does.Contain("Collections.GET"));
        Assert.That(efetivas, Does.Not.Contain("Collections.DELETE"));
    }

    [Test]
    public void Marcar_Separador_marca_as_areas_da_separacao_e_desmarcar_tira()
    {
        var grade = Grade();
        var separador = new ProfessionalRoleOption { Name = "Separator" };
        var funcoes = new List<ProfessionalRoleOption> { separador };

        separador.IsChecked = true;
        ProfessionalRolePresets.Apply(grade, funcoes, separador);

        var colecoes = Linha(grade, "Collections");
        Assert.That(colecoes.CanView && colecoes.CanCreate && colecoes.CanEdit, Is.True);
        Assert.That(colecoes.CanDelete, Is.False);
        Assert.That(Linha(grade, "Separator").CanEdit, Is.True);
        Assert.That(Linha(grade, "IdManagement").CanCreate, Is.True, "Gestão de IDs faz parte da separação");
        Assert.That(Linha(grade, "IdManagement").CanDelete, Is.False);
        Assert.That(Linha(grade, "Invoices").AnyChecked, Is.False, "não mexe em área de fora");

        separador.IsChecked = false;
        ProfessionalRolePresets.Apply(grade, funcoes, separador);
        Assert.That(grade.Any(x => x.AnyChecked), Is.False);
    }

    [Test]
    public void Desmarcar_funcao_nao_mexe_no_que_ela_nao_libera()
    {
        var grade = Grade();
        var separador = new ProfessionalRoleOption { Name = "Separator" };
        Linha(grade, "Collections").CanDelete = true; // marcado à mão
        Linha(grade, "Invoices").CanView = true;

        separador.IsChecked = true;
        ProfessionalRolePresets.Apply(grade, new[] { separador }, separador);
        separador.IsChecked = false;
        ProfessionalRolePresets.Apply(grade, new[] { separador }, separador);

        Assert.That(Linha(grade, "Collections").CanDelete, Is.True);
        Assert.That(Linha(grade, "Invoices").CanView, Is.True);
    }

    [Test]
    public void Callback_da_funcao_dispara_ao_marcar()
    {
        var chamadas = 0;
        var funcao = new ProfessionalRoleOption { Name = "Separator", CheckedChanged = _ => chamadas++ };
        funcao.IsChecked = true;
        funcao.IsChecked = false;
        Assert.That(chamadas, Is.EqualTo(2));
    }

    [Test]
    public void Dashboard_libera_a_aba_Colecoes_para_quem_e_Separador()
    {
        var p = DashboardPermissions.From("professionals", "{\"Roles\":[\"Separator\"],\"Policies\":[]}");
        Assert.That(p.CanSeeCollections, Is.True);
        Assert.That(p.CanCreateCollection, Is.True);
        Assert.That(p.CanSeeInvoices, Is.False);
    }
}

// Administrador, Fotógrafo e a função "Usuário" escondida.
[TestFixture]
public class ProfessionalRolePresetsAdminPhotographerTests
{
    private static List<ProfessionalPolicyDomainRow> Grade() =>
        UserAuthorization.GetAllPolicyDomains().Select(d => ProfessionalPolicyDomainRow.From(d, d.Domain)).ToList();

    [Test]
    public void Administrador_libera_tudo_em_todas_as_areas()
    {
        var auth = new UserAuthorizationConfig { Roles = new HashSet<string> { "Admin" } };
        foreach (var d in UserAuthorization.GetAllPolicyDomains())
        {
            Assert.That(UserAuthorization.HasPolicy(auth, d.Domain + ".DELETE"), Is.True, d.Domain);
            Assert.That(UserAuthorization.HasPolicy(auth, d.Domain + ".ALL"), Is.True, d.Domain);
        }
        var p = DashboardPermissions.From("professionals", "{\"Roles\":[\"Admin\"]}");
        Assert.That(p.HasNoContentTabs, Is.False);
        Assert.That(p.CanSeeInvoices && p.CanSeeCompanyConfig && p.CanManageProfessionals && p.CanCreateCollection, Is.True);
    }

    [Test]
    public void Fotografo_so_ve_e_sobe_colecoes()
    {
        Assert.That(UserAuthorization.GetPoliciesGrantedByRoles(new[] { "Photographer" }),
            Is.EquivalentTo(new[] { "Collections.GET", "Collections.POST" }));
        var p = DashboardPermissions.From("professionals", "{\"Roles\":[\"Photographer\"]}");
        Assert.That(p.CanSeeCollections && p.CanCreateCollection, Is.True);
        Assert.That(p.CanSeeInvoices || p.CanManageProfessionals || p.CanSeeCompanyConfig, Is.False);
    }

    [Test]
    public void Marcar_Administrador_marca_Tudo_e_desmarcar_mantem_o_do_Separador()
    {
        var grade = Grade();
        var admin = new ProfessionalRoleOption { Name = "Admin" };
        var separador = new ProfessionalRoleOption { Name = "Separator" };
        var funcoes = new List<ProfessionalRoleOption> { admin, separador };

        separador.IsChecked = true;
        ProfessionalRolePresets.Apply(grade, funcoes, separador);
        admin.IsChecked = true;
        ProfessionalRolePresets.Apply(grade, funcoes, admin);
        Assert.That(grade.All(x => x.CanAll), Is.True);

        admin.IsChecked = false;
        ProfessionalRolePresets.Apply(grade, funcoes, admin);
        var colecoes = grade.Single(x => x.Domain == "Collections");
        Assert.That(colecoes.CanView && colecoes.CanCreate && colecoes.CanEdit, Is.True, "Separador continua marcado");
        Assert.That(colecoes.CanDelete, Is.False);
        Assert.That(grade.Single(x => x.Domain == "Invoices").AnyChecked, Is.False);
    }

    [Test]
    public void Funcao_Usuario_nao_aparece_na_tela()
    {
        Assert.That(ProfessionalRolePresets.IsVisibleRole("user"), Is.False);
        Assert.That(ProfessionalRolePresets.IsVisibleRole("Separator"), Is.True);
        Assert.That(ProfessionalRolePresets.IsVisibleRole("Admin"), Is.True);
    }
}
