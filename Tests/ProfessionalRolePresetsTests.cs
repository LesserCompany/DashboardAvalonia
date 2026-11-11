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
    public void Separador_libera_ver_criar_e_editar_de_colecoes_e_separador_sem_excluir()
    {
        var liberadas = UserAuthorization.GetPoliciesGrantedByRoles(new[] { "Separator" });
        Assert.That(liberadas, Is.EquivalentTo(new[]
        {
            "Collections.GET", "Collections.POST", "Collections.PUT",
            "Separator.GET", "Separator.POST", "Separator.PUT",
        }));
        Assert.That(UserAuthorization.GetPoliciesGrantedByRoles(new[] { "Photographer", "user" }), Is.Empty, "funções sem tabela não liberam nada");
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
