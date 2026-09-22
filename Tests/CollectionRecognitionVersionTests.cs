using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class CollectionRecognitionVersionTests
{
    // [FIX DE EMERGÊNCIA — 2026-09-22] Invertido temporariamente junto com CollectionRecognitionVersion.Current
    // (ver comentário lá) — reverter os dois juntos quando o V2 estiver calibrado.
    [Test]
    public void Current_sempre_e_1_0()
    {
        Assert.That(CollectionRecognitionVersion.Current, Is.EqualTo("1.0"));
        Assert.That(CollectionRecognitionVersion.Current, Is.Not.EqualTo("2.0"));
    }
}
