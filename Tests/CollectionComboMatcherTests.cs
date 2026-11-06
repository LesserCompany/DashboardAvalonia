using LesserDashboardClient.Models;
using LesserDashboardClient.Services;
using NUnit.Framework;
using SharedClientSide.ServerInteraction;

namespace LesserDashboardClient.Tests;

// Mesma regra do fechamento da fatura (PgService.ComboFeaturesMatch): as 9 opções iguais, null ≠ false, primeiro que bater.
[TestFixture]
public class CollectionComboMatcherTests
{
    private static CollectionComboOptions ComboHdTratamento() => new()
    {
        ComboTitle = "Reconhecimento facial + Visualização online + Tratamento com IA",
        BackupHd = true,
        AutoTreatment = true,
        AllowDeletedProductionToBeFoundAnyone = true,
        PhotosDistribution = true,
    };

    private static CollectionComboOptions ComboSoReconhecimento() => new()
    {
        ComboTitle = "Reconhecimento facial",
        PhotosDistribution = true,
    };

    // Coleção criada pelo combo: todas as opções gravadas (PreConfiguredComboProfessionalTaskAuthority).
    private static ProfessionalTask ColecaoDoCombo(CollectionComboOptions c) => new()
    {
        UploadHD = c.BackupHd,
        AutoTreatment = c.AutoTreatment,
        OCR = c.Ocr,
        UploadPhotosAreAlreadySorted = c.UploadedPhotosAreAlreadySorted,
        AllowCPFsToSeeAllPhotos = c.AllowCPFsToSeeAllPhotos,
        EnablePhotosSales = c.EnablePhotoSales,
        AllowDeletedProductionToBeFoundAnyone = c.AllowDeletedProductionToBeFoundAnyone,
        PhotosDistribution = c.PhotosDistribution,
        EnableFaceRelevanceDetection = c.EnableFaceRelevanceDetection,
    };

    [Test]
    public void Colecao_com_as_opcoes_do_combo_mostra_o_combo()
    {
        var combos = new[] { ComboSoReconhecimento(), ComboHdTratamento() };
        var achado = CollectionComboMatcher.FindCombo(ColecaoDoCombo(combos[1]), combos);
        Assert.That(achado, Is.SameAs(combos[1]));
    }

    [Test]
    public void Uma_opcao_diferente_nao_e_combo()
    {
        var combo = ComboHdTratamento();
        var pt = ColecaoDoCombo(combo);
        pt.OCR = true;
        Assert.That(CollectionComboMatcher.FindCombo(pt, new[] { combo }), Is.Null);
    }

    [Test]
    public void Opcao_nula_nao_conta_como_false()
    {
        var combo = ComboSoReconhecimento();
        var pt = ColecaoDoCombo(combo);
        pt.EnableFaceRelevanceDetection = null;
        Assert.That(CollectionComboMatcher.FindCombo(pt, new[] { combo }), Is.Null);
    }

    [Test]
    public void Sem_combos_ou_sem_colecao_nao_mostra_nada()
    {
        Assert.That(CollectionComboMatcher.FindCombo(null, new[] { ComboHdTratamento() }), Is.Null);
        Assert.That(CollectionComboMatcher.FindCombo(ColecaoDoCombo(ComboHdTratamento()), null), Is.Null);
        Assert.That(CollectionComboMatcher.FindCombo(ColecaoDoCombo(ComboHdTratamento()), Array.Empty<CollectionComboOptions>()), Is.Null);
    }

    [Test]
    public void Dois_combos_com_as_mesmas_opcoes_vale_o_primeiro()
    {
        var primeiro = ComboHdTratamento();
        var segundo = ComboHdTratamento();
        Assert.That(CollectionComboMatcher.FindCombo(ColecaoDoCombo(segundo), new[] { primeiro, segundo }), Is.SameAs(primeiro));
    }
}
