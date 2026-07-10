using ALMOXPRO.Application.Interfaces;
using QRCoder;

namespace ALMOXPRO.Infrastructure.Codes;

public class QrCodeGenerator : IQrCodeGenerator
{
    public byte[] GeneratePng(string content, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
