using System.Text.Json;
using EeccStudio.Core.Network;
using EeccStudio.Core.Pairing;
using ZXing;
using ZXing.Common;

var directory = args.Length > 0 ? args[0] : Path.GetTempPath();
Directory.CreateDirectory(directory);

using var server = new CameraServer(0);
if (!server.TryStart(out var detail))
{
    Console.Error.WriteLine(detail);
    Environment.Exit(1);
}

var ticket = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
var json = ticket.ToQrJson();
var png = PairingQr.ToPng(json);
var pngPath = Path.Combine(directory, "studio-pairing.png");
File.WriteAllBytes(pngPath, png);
var decoded = Decode(png);
if (decoded != json)
{
    Console.Error.WriteLine("Le PNG ne redonne pas le JSON du Studio.");
    Environment.Exit(2);
}

var frameSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
server.OnCameraFrameReceived += (_, frame) =>
{
    if (frame.Length >= 2 && frame[0] == 0xFF && frame[1] == 0xD8)
        frameSeen.TrySetResult();
};

Console.WriteLine(json);
Console.WriteLine(decoded);
Console.WriteLine(server.ListeningPort);
Console.WriteLine(pngPath);
Console.Out.Flush();

await frameSeen.Task.WaitAsync(TimeSpan.FromSeconds(20));
Console.WriteLine("FRAME");
Console.Out.Flush();

static string Decode(byte[] png)
{
    var pixels = PngGray.Read(png);
    var rgb = new byte[pixels.Gray.Length * 3];
    for (var i = 0; i < pixels.Gray.Length; i++)
    {
        rgb[i * 3] = pixels.Gray[i];
        rgb[(i * 3) + 1] = pixels.Gray[i];
        rgb[(i * 3) + 2] = pixels.Gray[i];
    }

    var reader = new BarcodeReaderGeneric
    {
        Options = new DecodingOptions
        {
            TryHarder = true,
            PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
        },
    };
    var source = new RGBLuminanceSource(rgb, pixels.Width, pixels.Height, RGBLuminanceSource.BitmapFormat.RGB24);
    return reader.Decode(source)?.Text ?? string.Empty;
}

static class PngGray
{
    public static (byte[] Gray, int Width, int Height) Read(byte[] png)
    {
        var offset = 8;
        byte[]? inflated = null;
        var width = 0;
        var height = 0;
        while (offset + 8 <= png.Length)
        {
            var length = ReadInt(png, offset);
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            var data = png.AsSpan(offset + 8, length).ToArray();
            if (type == "IHDR")
            {
                width = ReadInt(data, 0);
                height = ReadInt(data, 4);
            }
            else if (type == "IDAT")
            {
                using var input = new MemoryStream(data);
                using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                inflated = output.ToArray();
            }
            else if (type == "IEND")
            {
                break;
            }

            offset += 12 + length;
        }

        if (inflated == null)
            throw new InvalidDataException("PNG sans image.");
        var gray = new byte[width * height];
        var row = 0;
        for (var y = 0; y < height; y++)
        {
            row++;
            Buffer.BlockCopy(inflated, row, gray, y * width, width);
            row += width;
        }

        return (gray, width, height);
    }

    private static int ReadInt(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
}
