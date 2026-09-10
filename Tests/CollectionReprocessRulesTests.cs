using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class CollectionReprocessRulesTests
{
    [Test]
    public void Sem_progresso_nao_mostra_botao()
    {
        Assert.That(CollectionReprocessRules.ShouldShowReprocessButton(false, false), Is.False);
        Assert.That(CollectionReprocessRules.ShouldShowReprocessButton(false, true), Is.False);
    }

    [Test]
    public void Completo_a_100_porcento_esconde_botao()
    {
        Assert.That(
            CollectionReprocessRules.IsFullyComplete(total: 10, done: 10, ocrDone: 0, autoTreatedDone: 0, ocrEnabled: false, autoTreatmentEnabled: false),
            Is.True);
        Assert.That(CollectionReprocessRules.ShouldShowReprocessButton(true, true), Is.False);
    }

    [Test]
    public void Reconhecimento_incompleto_mostra_botao()
    {
        var complete = CollectionReprocessRules.IsFullyComplete(
            total: 10, done: 7, ocrDone: 0, autoTreatedDone: 0, ocrEnabled: false, autoTreatmentEnabled: false);

        Assert.That(complete, Is.False);
        Assert.That(CollectionReprocessRules.ShouldShowReprocessButton(true, complete), Is.True);
    }

    [Test]
    public void Rec_completo_mas_ocr_pendente_mostra_botao()
    {
        var complete = CollectionReprocessRules.IsFullyComplete(
            total: 8, done: 8, ocrDone: 3, autoTreatedDone: 8, ocrEnabled: true, autoTreatmentEnabled: true);

        Assert.That(complete, Is.False);
        Assert.That(CollectionReprocessRules.ShouldShowReprocessButton(true, complete), Is.True);
    }

    [Test]
    public void Rec_completo_mas_tratamento_pendente_mostra_botao()
    {
        var complete = CollectionReprocessRules.IsFullyComplete(
            total: 8, done: 8, ocrDone: 8, autoTreatedDone: 2, ocrEnabled: true, autoTreatmentEnabled: true);

        Assert.That(complete, Is.False);
        Assert.That(CollectionReprocessRules.ShouldShowReprocessButton(true, complete), Is.True);
    }

    [Test]
    public void Total_zero_considera_completo()
    {
        Assert.That(
            CollectionReprocessRules.IsFullyComplete(total: 0, done: 0, ocrDone: 0, autoTreatedDone: 0, ocrEnabled: true, autoTreatmentEnabled: true),
            Is.True);
    }

    [Test]
    public void Rec_ocr_e_tratamento_completos_esconde_botao()
    {
        var complete = CollectionReprocessRules.IsFullyComplete(
            total: 5, done: 5, ocrDone: 5, autoTreatedDone: 5, ocrEnabled: true, autoTreatmentEnabled: true);

        Assert.That(complete, Is.True);
        Assert.That(CollectionReprocessRules.ShouldShowReprocessButton(true, complete), Is.False);
    }
}
