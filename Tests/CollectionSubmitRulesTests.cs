using LesserDashboardClient.Helpers;
using NUnit.Framework;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class CollectionSubmitRulesTests
{
    [Test]
    public void Salvar_nao_inicia_app_de_upload_nem_envia_fotos()
    {
        Assert.That(CollectionSubmitRules.StartsUploadApp(saveOnly: true), Is.False);
        Assert.That(CollectionSubmitRules.SendsPhotoFileLists(saveOnly: true), Is.False);
        Assert.That(CollectionSubmitRules.RequiresLocalPhotoFolders(saveOnly: true), Is.False);
    }

    [Test]
    public void Reupload_inicia_app_de_upload_e_envia_fotos()
    {
        Assert.That(CollectionSubmitRules.StartsUploadApp(saveOnly: false), Is.True);
        Assert.That(CollectionSubmitRules.SendsPhotoFileLists(saveOnly: false), Is.True);
        Assert.That(CollectionSubmitRules.RequiresLocalPhotoFolders(saveOnly: false), Is.True);
    }
}
