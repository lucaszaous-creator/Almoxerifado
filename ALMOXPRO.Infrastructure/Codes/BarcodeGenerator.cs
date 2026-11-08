using ALMOXPRO.Application.Interfaces;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ZXing;
using ZXing.Common;

namespace ALMOXPRO.Infrastructure.Codes;

/// <summary>Gera códigos de barras Code128 em PNG usando ZXing.Net.</summary>
[SupportedOSPlatform("windows")]
public class BarcodeGenerator : IBarcodeGenerator
{
    public byte[] GeneratePng(string content, int width = 300, int height = 100)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 2,
                PureBarcode = false
            }
        };

        var pixelData = writer.Write(content);

        using var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, pixelData.Width, pixelData.Height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try
        {
            Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
