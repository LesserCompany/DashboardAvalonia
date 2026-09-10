using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class CollectionsListRefreshSettingsTests
{
    [Test]
    public void Intervalo_e_10_minutos()
    {
        Assert.That(CollectionsListRefreshSettings.Interval, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }
}
