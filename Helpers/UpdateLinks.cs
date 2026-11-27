namespace LesserDashboardClient.Helpers;

/// <summary>Destinos dos botões da tela de instruções de atualização.</summary>
public static class UpdateLinks
{
    /// <summary>Instalador novo para quem não veio da Microsoft Store.</summary>
    public const string ZipDownloadUrl = "https://letspic.com.br/letspic.zip";

    /// <summary>Product Id na Microsoft Store — https://apps.microsoft.com/detail/9P5GDKBRXR16</summary>
    public const string StoreProductId = "9P5GDKBRXR16";

    public const string StoreProductPageUri = "ms-windows-store://pdp/?ProductId=" + StoreProductId;

    /// <summary>Configurações do Windows › Aplicativos instalados (onde se desinstala o app).</summary>
    public const string InstalledAppsSettingsUri = "ms-settings:appsfeatures";
}
