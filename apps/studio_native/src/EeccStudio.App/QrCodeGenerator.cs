using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EeccStudio.Core.Pairing;

namespace EeccStudio;

/// <summary>
/// Affiche le PNG QR produit par Studio : modules entiers, noir et blanc, zone calme de 4 modules.
/// </summary>
public static class QrCodeGenerator
{
    public static BitmapSource GenerateQrCode(string text, int pixelSize = 280)
    {
        var png = PairingQr.ToPng(text);
        using var stream = new MemoryStream(png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        RenderOptions.SetBitmapScalingMode(bitmap, BitmapScalingMode.NearestNeighbor);
        return bitmap;
    }
}