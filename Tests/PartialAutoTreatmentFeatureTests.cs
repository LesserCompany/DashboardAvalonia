using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class PartialAutoTreatmentFeatureTests
{
    [Test]
    public void Tratamento_parcial_fica_oculto()
    {
        Assert.That(PartialAutoTreatmentFeature.Enabled, Is.False);
        Assert.That(PartialAutoTreatmentFeature.ShowInList(hasBatch: true), Is.False);
        Assert.That(PartialAutoTreatmentFeature.ShowPanel(isHdWithoutFullTreatment: true), Is.False);
    }
}
