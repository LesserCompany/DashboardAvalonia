using NUnit.Framework;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users;

namespace LesserDashboardClient.Tests;

// Criar coleção mostrava "Unexpected character encountered while parsing value: e. Path '', line 0, position 0."
// quando o servidor respondia texto em vez de JSON: a mensagem real do servidor se perdia.
[TestFixture]
public class LesserFunctionJsonResponseTests
{
    [Test]
    public void TryParse_le_json_valido()
    {
        var ok = LesserFunctionJsonResponse.TryParse<UserResult>("{\"success\":true,\"message\":\"Success.\"}", out var r);

        Assert.That(ok, Is.True);
        Assert.That(r!.success, Is.True);
        Assert.That(r.message, Is.EqualTo("Success."));
    }

    [Test]
    public void TryParse_nao_lanca_quando_a_resposta_e_texto()
    {
        var ok = LesserFunctionJsonResponse.TryParse<UserResult>("error: falha ao gravar", out _);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void ToFailure_devolve_UserResult_com_o_texto_e_o_status_do_servidor()
    {
        var r = LesserFunctionJsonResponse.ToFailure<UserResult>("error: falha ao gravar", 500, "Internal Server Error");

        Assert.That(r.success, Is.False);
        Assert.That(r.loginFailed, Is.Not.True);
        Assert.That(r.message, Does.Contain("error: falha ao gravar"));
        Assert.That(r.message, Does.Contain("500"));
    }

    [Test]
    public void ToFailure_funciona_com_UserResult_generico()
    {
        var r = LesserFunctionJsonResponse.ToFailure<UserResult<string>>("erro interno", 502, "Bad Gateway");

        Assert.That(r.success, Is.False);
        Assert.That(r.message, Does.Contain("erro interno"));
        Assert.That(r.message, Does.Contain("502"));
    }

    [Test]
    public void ToFailure_corta_resposta_muito_longa()
    {
        var body = new string('x', 5000);

        var r = LesserFunctionJsonResponse.ToFailure<UserResult>(body, 500, "Internal Server Error");

        Assert.That(r.message.Length, Is.LessThan(LesserFunctionJsonResponse.MaxBodyCharsInMessage + 200));
    }

    [Test]
    public void ToFailure_lanca_excecao_com_o_texto_do_servidor_quando_o_tipo_nao_e_resultado()
    {
        var ex = Assert.Throws<LesserFunctionInvalidResponseException>(
            () => LesserFunctionJsonResponse.ToFailure<string>("error: algo", 500, "Internal Server Error"));

        Assert.That(ex!.Message, Does.Contain("error: algo"));
        Assert.That(ex.ResponseBody, Is.EqualTo("error: algo"));
        Assert.That(ex.StatusCode, Is.EqualTo(500));
    }
}
