using NUnit.Framework;
using SharedClientSide.ServerInteraction.Users.Login;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class LoginEnvironmentResolverTests
{
    const string Bootstrap = "https://functions.lesser.biz/api/";

    [Test]
    public void Username_vazio_vai_como_test()
    {
        Assert.That(LoginEnvironmentResolver.CompanyUsernameOrTest(null), Is.EqualTo("test"));
        Assert.That(LoginEnvironmentResolver.CompanyUsernameOrTest("   "), Is.EqualTo("test"));
    }

    [Test]
    public void Username_digitado_e_mantido()
    {
        Assert.That(LoginEnvironmentResolver.CompanyUsernameOrTest("  empresa  "), Is.EqualTo("empresa"));
    }

    [Test]
    public void Http_200_com_functionsEndpointAddress_troca_a_base()
    {
        var url = LoginEnvironmentResolver.Resolve(
            Bootstrap,
            httpOk: true,
            success: true,
            functionsEndpointAddress: "https://alpha.example/api/");

        Assert.That(url, Is.EqualTo("https://alpha.example/api/"));
    }

    [Test]
    public void Falha_de_rede_http_ou_success_mantem_o_bootstrap()
    {
        Assert.That(LoginEnvironmentResolver.Resolve(Bootstrap, false, true, "https://alpha.example/api/"), Is.EqualTo(Bootstrap));
        Assert.That(LoginEnvironmentResolver.Resolve(Bootstrap, true, false, "https://alpha.example/api/"), Is.EqualTo(Bootstrap));
        Assert.That(LoginEnvironmentResolver.Resolve(Bootstrap, true, true, "  "), Is.EqualTo(Bootstrap));
        Assert.That(LoginEnvironmentResolver.Resolve(Bootstrap, true, true, null), Is.EqualTo(Bootstrap));
    }
}
