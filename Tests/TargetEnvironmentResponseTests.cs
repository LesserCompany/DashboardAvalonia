using Newtonsoft.Json;
using NUnit.Framework;
using SharedClientSide.DataStructure.CoreApp.TargetEnvironment;

namespace LesserDashboardClient.Tests;

[TestFixture]
public class TargetEnvironmentResponseTests
{
    [Test]
    public void Le_functionUrl_e_gcloudUrl_da_resposta_do_GetTarget()
    {
        const string json = """
            {
              "functionUrl": "https://ss-lesser-functions-net8-v2.azurewebsites.net/api/v2/",
              "gcloudUrl": "https://updown.example.run.app/api/v2/",
              "folderDownloadApps": "/apps-alpha"
            }
            """;

        var parsed = JsonConvert.DeserializeObject<TargetEnvironmentResponse>(json);

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.FunctionsEndpointAddress, Is.EqualTo("https://ss-lesser-functions-net8-v2.azurewebsites.net/api/v2/"));
        Assert.That(parsed.GCloudEndpointAddress, Is.EqualTo("https://updown.example.run.app/api/v2/"));
        Assert.That(parsed.FolderDownloadApps, Is.EqualTo("/apps-alpha"));
    }

    [Test]
    public void Le_os_nomes_antigos_functionsEndpointAddress()
    {
        const string json = """
            {
              "functionsEndpointAddress": "https://functions.example/api/",
              "gCloudEndpointAddress": "https://gcloud.example/api/",
              "folderDownloadApps": "/apps"
            }
            """;

        var parsed = JsonConvert.DeserializeObject<TargetEnvironmentResponse>(json);

        Assert.That(parsed!.FunctionsEndpointAddress, Is.EqualTo("https://functions.example/api/"));
        Assert.That(parsed.GCloudEndpointAddress, Is.EqualTo("https://gcloud.example/api/"));
        Assert.That(parsed.FolderDownloadApps, Is.EqualTo("/apps"));
    }
}
