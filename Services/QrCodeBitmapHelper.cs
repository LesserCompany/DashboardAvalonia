using Avalonia.Media.Imaging;
using QRCoder;
using System.IO;

namespace LesserDashboardClient.Services;

public static class QrCodeBitmapHelper
{
    public static Bitmap CreateQrBitmap(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        var pngBytes = qrCode.GetGraphic(pixelsPerModule);
        using var stream = new MemoryStream(pngBytes);
        return new Bitmap(stream);
    }
}
