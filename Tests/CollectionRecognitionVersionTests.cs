using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class CollectionRecognitionVersionTests
{
    [Test]
    public void Current_sempre_e_2_0()
    {
        Assert.That(CollectionRecognitionVersion.Current, Is.EqualTo("2.0"));
        Assert.That(CollectionRecognitionVersion.Current, Is.Not.EqualTo("1.0"));
    }
}
