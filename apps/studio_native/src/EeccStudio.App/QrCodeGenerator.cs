using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EeccStudio.Core.Pairing;

namespace EeccStudio;

/// <summary>
/// Dessine la matrice QR produite par QRCoder. Ce n'est pas un motif décoratif.
/// </summary>
public static class QrCodeGenerator
{
    public static BitmapSource GenerateQrCode(string text, int pixelSize = 280)
    {
        var matrix = PairingQr.CreateMatrix(text);
        var modules = matrix.GetLength(0);
        const int quietModules = 4;
        var scale = Math.Max(2, pixelSize / (modules + (quietModules * 2)));
        var total = (modules + (quietModules * 2)) * scale;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, total, total));
            for (var y = 0; y < modules; y++)
            {
                for (var x = 0; x < modules; x++)
                {
                    if (!matrix[x, y])
                        continue;
                    var rect = new Rect((quietModules + x) * scale, (quietModules + y) * scale, scale, scale);
                    context.DrawRectangle(Brushes.Black, null, rect);
                }
            }
        }

        var bitmap = new RenderTargetBitmap(total, total, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
