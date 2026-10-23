using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class PartialAutoTreatmentFeatureTests
{
    [Test]
    public void Tratamento_parcial_aparece_no_painel_hd_e_na_lista_com_lote()
    {
        Assert.That(PartialAutoTreatmentFeature.Enabled, Is.True);
        Assert.That(PartialAutoTreatmentFeature.ShowInList(hasBatch: true), Is.True);
        Assert.That(PartialAutoTreatmentFeature.ShowInList(hasBatch: false), Is.False);
        Assert.That(PartialAutoTreatmentFeature.ShowPanel(isHdWithoutFullTreatment: true), Is.True);
        Assert.That(PartialAutoTreatmentFeature.ShowPanel(isHdWithoutFullTreatment: false), Is.False);
    }
}
