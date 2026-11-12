using QRCoder;

namespace EeccStudio.Core.Pairing;

public static class PairingQr
{
    public static byte[] ToPng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(8);
    }
}
