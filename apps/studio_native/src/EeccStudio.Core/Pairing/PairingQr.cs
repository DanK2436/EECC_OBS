using System.IO.Compression;
using QRCoder;

namespace EeccStudio.Core.Pairing;

public static class PairingQr
{
    public const int QuietModules = 4;
    public const int PixelsPerModule = 8;

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

    /// <summary>
    /// PNG 8 bits, noir et blanc purs, zone calme de 4 modules. Aucun gris d'anticrénelage.
    /// </summary>
    public static byte[] ToPng(string text, int pixelsPerModule = PixelsPerModule, int quietModules = QuietModules)
    {
        if (pixelsPerModule < 2)
            throw new ArgumentOutOfRangeException(nameof(pixelsPerModule));
        if (quietModules < QuietModules)
            throw new ArgumentOutOfRangeException(nameof(quietModules));

        var modules = CreateMatrix(text);
        var moduleCount = modules.GetLength(0);
        var size = (moduleCount + (quietModules * 2)) * pixelsPerModule;
        var raw = new byte[size * (size + 1)];
        var offset = 0;
        for (var y = 0; y < size; y++)
        {
            raw[offset++] = 0;
            var moduleY = (y / pixelsPerModule) - quietModules;
            for (var x = 0; x < size; x++)
            {
                var moduleX = (x / pixelsPerModule) - quietModules;
                var dark = moduleX >= 0 && moduleY >= 0 && moduleX < moduleCount && moduleY < moduleCount && modules[moduleX, moduleY];
                raw[offset++] = dark ? (byte)0 : (byte)255;
            }
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(png, "IHDR", Ihdr(size));
        using (var idat = new MemoryStream())
        {
            using (var zlib = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(raw);
            WriteChunk(png, "IDAT", idat.ToArray());
        }
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static byte[] Ihdr(int size)
    {
        var header = new byte[13];
        WriteInt(header, 0, size);
        WriteInt(header, 4, size);
        header[8] = 8;
        header[9] = 0;
        return header;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        stream.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc32(crcInput));
        stream.Write(crc);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return ~crc;
    }
}
