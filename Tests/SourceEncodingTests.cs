using System.Text;
using SharedClientSide.BuildTools;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

// Regras do verificador de encoding (SharedClientSide/BuildTools/SourceEncoding). A varredura dos arquivos do app é feita
// pela própria build (importada via SharedClientSide_AVALONIA.projitems), então aqui só se testam as regras.
[TestFixture]
public class SourceEncodingTests
{
    private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

    private static byte[] WithBom(string text) => Bom.Concat(Encoding.UTF8.GetBytes(text)).ToArray();

    [Test]
    public void Texto_normal_com_BOM_passa()
    {
        var problems = SourceEncodingScanner.CheckBytes(WithBom("// coleção — CORREÇÃO: não é"), "a.cs", ".cs");
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void Acento_corrompido_e_acusado()
    {
        var corrompido = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("coleção"));
        var problems = SourceEncodingScanner.CheckBytes(WithBom("var a = 1;\n// " + corrompido), "a.cs", ".cs").ToList();
        Assert.That(problems.Select(p => p.Line), Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public void Mensagem_de_acento_corrompido_diz_que_e_encoding_e_mostra_a_correcao()
    {
        var corrompido = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("Nenhum CPF nessa coleção — ok"));
        var problem = SourceEncodingScanner.CheckBytes(WithBom("// " + corrompido), "a.cs", ".cs").Single();
        Assert.That(problem.Code, Is.EqualTo("ENC001"));
        Assert.That(problem.Message, Does.Contain("NÃO é erro de código"));
        Assert.That(problem.Message, Does.Contain("Nenhum CPF nessa coleção — ok"));
    }

    [Test]
    public void Arquivo_em_ANSI_aponta_a_linha_do_primeiro_byte_invalido()
    {
        var ansi = Bom.Concat(Encoding.UTF8.GetBytes("var a = 1;\nvar b = 2;\n")).Concat(Encoding.Latin1.GetBytes("// não")).ToArray();
        var problem = SourceEncodingScanner.CheckBytes(ansi, "a.cs", ".cs").Single();
        Assert.That(problem.Line, Is.EqualTo(3));
        Assert.That(problem.Code, Is.EqualTo("ENC003"));
    }

    [Test]
    public void Caractere_perdido_e_acusado()
    {
        var problems = SourceEncodingScanner.CheckBytes(WithBom("// N" + (char)0xFFFD + "o"), "a.cs", ".cs").ToList();
        Assert.That(problems, Has.Count.EqualTo(1));
        Assert.That(problems[0].Message, Does.Contain("U+FFFD"));
    }

    [Test]
    public void Arquivo_salvo_em_ANSI_e_acusado()
    {
        var ansi = Bom.Concat(Encoding.Latin1.GetBytes("// não")).ToArray();
        var problems = SourceEncodingScanner.CheckBytes(ansi, "a.cs", ".cs").ToList();
        Assert.That(problems.Single().Code, Is.EqualTo("ENC003"));
    }

    [TestCase(".cs")]
    [TestCase(".axaml")]
    public void Codigo_e_xaml_sem_BOM_sao_acusados(string extension)
    {
        var problems = SourceEncodingScanner.CheckBytes(Encoding.UTF8.GetBytes("// não"), "a" + extension, extension).ToList();
        Assert.That(problems.Single().Message, Does.Contain("BOM"));
    }

    [Test]
    public void Json_de_traducao_nao_exige_BOM()
    {
        var problems = SourceEncodingScanner.CheckBytes(Encoding.UTF8.GetBytes("{\"a\":\"não\"}"), "x.loc.json", ".json");
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void App_pode_escolher_quais_extensoes_exigem_BOM()
    {
        var problems = SourceEncodingScanner.CheckBytes(Encoding.UTF8.GetBytes("{}"), "x.json", ".json", new[] { ".json" }).ToList();
        Assert.That(problems.Single().Code, Is.EqualTo("ENC004"));
    }
}
