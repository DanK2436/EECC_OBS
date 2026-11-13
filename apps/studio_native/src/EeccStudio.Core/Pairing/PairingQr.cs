using QRCoder;

namespace EeccStudio.Core.Pairing;

public static class PairingQr
{
    public static bool[,] CreateMatrix(string text)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Le QR n'a pas de contenu.", nameof(text));

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M, forceUtf8: true);
        var size = data.ModuleMatrix.Count;
        var matrix = new bool[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
                matrix[x, y] = data.ModuleMatrix[y][x];
        }

        return matrix;
    }
}
