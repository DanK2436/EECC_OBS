using System.IO.Compression;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EeccStudio.Core.Pairing;
using ZXing;
using ZXing.Common;

namespace EeccStudio.Core.Tests;

public class PairingContractTests
{
    [Fact]
    public void Qr_document_roundtrip_and_real_png()
    {
        var ticket = PairingTicket.Issue("10.1.2.3", 8091, new string('a', 64), TimeSpan.FromMinutes(3), DateTimeOffset.UnixEpoch.AddHours(10));
        var payload = ticket.ToQrJson();
        Assert.True(PairingTicket.TryParse(payload, out var parsed, out var error), error);
        Assert.NotNull(parsed);
        Assert.Equal(ticket.Ip, parsed!.Ip);
        Assert.Equal(ticket.Port, parsed.Port);
        Assert.Equal(ticket.ExpiresAt, parsed.ExpiresAt);
        Assert.Equal(ticket.CertSha256, parsed.CertSha256);
        Assert.Null(PairingTicket.Evaluate(parsed, ticket.Token, ticket.ExpiresAt.AddSeconds(-1), false));

        var png = PairingQr.ToPng(payload);
        Assert.True(png.Length > 100);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4));
        var (pixels, width, height) = DecodeGray8(png);
        var reader = new BarcodeReaderGeneric { AutoRotate = true, Options = { PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE } } };
        var decoded = reader.Decode(new RGBLuminanceSource(pixels, width, height, RGBLuminanceSource.BitmapFormat.Gray8));
        Assert.True(decoded != null && decoded.Text == payload, "Le QR décodé ne correspond pas au document d'appairage.");
    }

    [Fact]
    public void Expired_invalid_and_reused_tokens_are_rejected()
    {
        var ticket = PairingTicket.Issue("192.168.8.4", 8091, new string('b', 64), now: DateTimeOffset.UnixEpoch.AddHours(2));
        Assert.Equal("expired", PairingTicket.Evaluate(ticket, ticket.Token, ticket.ExpiresAt.AddSeconds(1), false));
        Assert.Equal("invalid", PairingTicket.Evaluate(ticket, new string('c', 32), ticket.ExpiresAt.AddSeconds(-1), false));
        Assert.Equal("used", PairingTicket.Evaluate(ticket, ticket.Token, ticket.ExpiresAt.AddSeconds(-1), true));
        Assert.Equal("invalid", PairingTicket.Evaluate(null, ticket.Token, DateTimeOffset.UtcNow, false));
        Assert.False(PairingTicket.TryParse("{\"studio_ip\":\"192.168.1.50\",\"token\":\"demo_token_123\"}", out _, out var error));
        Assert.Equal("invalid", error);
        Assert.False(PairingTicket.TryParse(ticket.ToQrJson().Replace("\"v\":1", "\"v\":2"), out _, out error));
        Assert.Equal("version", error);
        Assert.DoesNotContain("token", ticket.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signaling_rejects_bad_fingerprint_expired_invalid_and_reuse()
    {
        var certificate = StudioCertificate.Create();
        var fingerprint = StudioCertificate.Fingerprint(certificate);
        var ticket = PairingTicket.Issue("10.8.0.6", 8091, fingerprint, TimeSpan.FromMinutes(3));
        var consumed = false;
        var statuses = new List<string>();
        var server = new SignalingServer(token =>
        {
            var reason = PairingTicket.Evaluate(ticket, token, DateTimeOffset.UtcNow, consumed);
            if (reason == null) consumed = true;
            return reason;
        }, _ => Task.CompletedTask);
        server.StatusChanged += statuses.Add;
        await server.StartAsync(certificate, 0);

        await Assert.ThrowsAnyAsync<Exception>(() => HelloAsync(server.Port, new string('d', 64), ticket.Token));

        var invalid = await HelloAsync(server.Port, fingerprint, new string('e', 32));
        Assert.Contains("invalid", invalid, StringComparison.Ordinal);
        var expiredTicket = PairingTicket.Issue("10.8.0.6", server.Port, fingerprint, TimeSpan.FromSeconds(-5));
        await using var expiredServer = new SignalingServer(
            token => PairingTicket.Evaluate(expiredTicket, token, DateTimeOffset.UtcNow, false),
            _ => Task.CompletedTask);
        await expiredServer.StartAsync(certificate, 0);
        var expired = await HelloAsync(expiredServer.Port, fingerprint, expiredTicket.Token);
        Assert.Contains("expired", expired, StringComparison.Ordinal);

        var accepted = await HelloAsync(server.Port, fingerprint, ticket.Token);
        Assert.Contains("hello-ok", accepted, StringComparison.Ordinal);
        var reused = await HelloAsync(server.Port, fingerprint, ticket.Token);
        Assert.Contains("used", reused, StringComparison.Ordinal);
        Assert.DoesNotContain(statuses, status => status.Contains(ticket.Token, StringComparison.Ordinal));
        await server.DisposeAsync();
    }

    private static (byte[] Pixels, int Width, int Height) DecodeGray8(byte[] png)
    {
        var offset = 8;
        var width = 0;
        var height = 0;
        var bitDepth = 0;
        var colorType = 0;
        using var data = new MemoryStream();
        while (offset + 8 <= png.Length)
        {
            var length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var chunk = png.AsSpan(offset + 8, length);
            if (type == "IHDR")
            {
                width = (chunk[0] << 24) | (chunk[1] << 16) | (chunk[2] << 8) | chunk[3];
                height = (chunk[4] << 24) | (chunk[5] << 16) | (chunk[6] << 8) | chunk[7];
                bitDepth = chunk[8];
                colorType = chunk[9];
            }
            else if (type == "IDAT")
            {
                data.Write(chunk);
            }
            else if (type == "IEND")
            {
                break;
            }
            offset += 12 + length;
        }

        var compressed = data.ToArray();
        using var deflate = new DeflateStream(new MemoryStream(compressed, 2, compressed.Length - 6), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        deflate.CopyTo(raw);
        var scan = raw.ToArray();
        var rowBytes = bitDepth == 1 ? (width + 7) / 8 : width;
        var pixels = new byte[width * height];
        var previous = new byte[rowBytes];
        var cursor = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = scan[cursor++];
            var row = new byte[rowBytes];
            for (var i = 0; i < rowBytes; i++)
            {
                var value = scan[cursor++];
                var left = i > 0 ? row[i - 1] : 0;
                var up = previous[i];
                var upLeft = i > 0 ? previous[i - 1] : 0;
                row[i] = (byte)(value + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException("filtre PNG"),
                });
            }
            previous = row;
            if (bitDepth == 1)
            {
                for (var x = 0; x < width; x++)
                {
                    var bit = (row[x / 8] >> (7 - (x % 8))) & 1;
                    pixels[y * width + x] = bit == 1 ? (byte)255 : (byte)0;
                }
            }
            else if (bitDepth == 8 && colorType == 0)
            {
                Buffer.BlockCopy(row, 0, pixels, y * width, width);
            }
            else
            {
                throw new InvalidDataException("PNG QR non pris en charge");
            }
        }
        return (pixels, width, height);
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);
        if (leftDistance <= upDistance && leftDistance <= upLeftDistance) return left;
        return upDistance <= upLeftDistance ? up : upLeft;
    }

    private static async Task<string> HelloAsync(int port, string fingerprint, string token)
    {
        using var socket = new ClientWebSocket();
        socket.Options.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
            certificate is X509Certificate2 cert && StudioCertificate.Fingerprint(cert) == fingerprint;
        await socket.ConnectAsync(new Uri("wss://127.0.0.1:" + port + "/pair"), CancellationToken.None);
        var hello = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"v\":1,\"token\":\"" + token + "\"}");
        await socket.SendAsync(hello, WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[1024];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
