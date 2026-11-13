using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace EeccStudio.Broadcast;

/// <summary>
/// Contrat HTTP déjà lu par EECC TV : /live/status, /live/frame.jpg et /live/stream.
/// Aucune adresse d'ingestion publique n'est inventée.
/// </summary>
public static class EeccTvContract
{
    public const string StatusPath = "/live/status";
    public const string FramePath = "/live/frame.jpg";
    public const string StreamPath = "/live/stream";
    public const string Boundary = "eecc_boundary_frame";

    public static byte[] StatusJson(
        bool isLive,
        string playbackUrl,
        string title,
        string speaker,
        string scene,
        int viewers)
    {
        var payload = new
        {
            service = "EECC Studio Broadcast Server",
            is_live = isLive,
            status = isLive ? "live" : "standby",
            title,
            speaker,
            active_scene = scene,
            live_stream_url = playbackUrl,
            transport = "mjpeg-http",
            viewers
        };
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    public static async Task WriteJsonAsync(Stream stream, byte[] json, CancellationToken cancellationToken)
    {
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Cache-Control: no-store\r\n" +
            $"Content-Length: {json.Length}\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(json, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task WriteOfflineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes("{\"error\":\"hors_antenne\",\"is_live\":false}");
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 404 Not Found\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Cache-Control: no-store\r\n" +
            $"Content-Length: {body.Length}\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task WriteJpegAsync(Stream stream, byte[] jpeg, CancellationToken cancellationToken)
    {
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: image/jpeg\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Cache-Control: no-store\r\n" +
            $"Content-Length: {jpeg.Length}\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(jpeg, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task WriteMjpegAsync(
        Stream stream,
        Func<bool> isLive,
        Func<byte[]> frame,
        Func<bool> keepGoing,
        CancellationToken cancellationToken)
    {
        if (!isLive())
        {
            await WriteOfflineAsync(stream, cancellationToken);
            return;
        }

        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            $"Content-Type: multipart/x-mixed-replace; boundary=--{Boundary}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Cache-Control: no-cache, no-store, must-revalidate\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);

        while (!cancellationToken.IsCancellationRequested && keepGoing() && isLive())
        {
            var jpeg = frame();
            var part = Encoding.ASCII.GetBytes(
                $"--{Boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n");
            await stream.WriteAsync(part, cancellationToken);
            await stream.WriteAsync(jpeg, cancellationToken);
            await stream.WriteAsync("\r\n"u8.ToArray(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            await Task.Delay(66, cancellationToken);
        }
    }

    public static async Task<string> ReadRequestPathAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var received = 0;
        while (received < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(received, buffer.Length - received), cancellationToken);
            if (read == 0)
                break;
            received += read;
            var text = Encoding.ASCII.GetString(buffer, 0, received);
            var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
                continue;
            var first = text.Split('\r', '\n')[0];
            var parts = first.Split(' ');
            if (parts.Length < 2)
                return "/";
            var path = parts[1];
            var query = path.IndexOf('?', StringComparison.Ordinal);
            return query >= 0 ? path[..query] : path;
        }

        return "/";
    }
}

public static class EeccTvFrames
{
    // JPEG 160×120 décodable (produit par ffmpeg). L'ancienne image minimale n'avait pas de taille
    // exploitable par l'encodeur externe.
    public static readonly byte[] Slate =
    {
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02, 0x00, 0x00, 0x01,
        0x00, 0x01, 0x00, 0x00, 0xFF, 0xFE, 0x00, 0x10, 0x4C, 0x61, 0x76, 0x63, 0x36, 0x30, 0x2E, 0x33,
        0x31, 0x2E, 0x31, 0x30, 0x32, 0x00, 0xFF, 0xDB, 0x00, 0x43, 0x00, 0x08, 0x0A, 0x0A, 0x0B, 0x0A,
        0x0B, 0x0D, 0x0D, 0x0D, 0x0D, 0x0D, 0x0D, 0x10, 0x0F, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10,
        0x10, 0x10, 0x10, 0x10, 0x12, 0x12, 0x12, 0x15, 0x15, 0x15, 0x12, 0x12, 0x12, 0x10, 0x10, 0x12,
        0x12, 0x14, 0x14, 0x15, 0x15, 0x17, 0x17, 0x17, 0x15, 0x15, 0x15, 0x15, 0x17, 0x17, 0x19, 0x19,
        0x19, 0x1E, 0x1E, 0x1C, 0x1C, 0x23, 0x23, 0x24, 0x2B, 0x2B, 0x33, 0xFF, 0xC4, 0x00, 0x4D, 0x00,
        0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x06, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x06, 0x07, 0x10, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x11, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00,
        0x78, 0x00, 0xA0, 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00, 0xFF, 0xDA, 0x00,
        0x0C, 0x03, 0x01, 0x00, 0x02, 0x11, 0x03, 0x11, 0x00, 0x3F, 0x00, 0x8E, 0x01, 0xA6, 0xA7, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0xFF, 0xD9
    };

    public static byte[] WithComment(byte[] jpeg, string comment)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new ArgumentException("Une image JPEG est requise.", nameof(jpeg));

        var text = Encoding.ASCII.GetBytes(comment);
        if (text.Length > 60000)
            throw new ArgumentException("Le commentaire est trop long.", nameof(comment));

        var length = text.Length + 2;
        var result = new byte[jpeg.Length + 4 + text.Length];
        result[0] = 0xFF;
        result[1] = 0xD8;
        result[2] = 0xFF;
        result[3] = 0xFE;
        result[4] = (byte)(length >> 8);
        result[5] = (byte)(length & 0xFF);
        Buffer.BlockCopy(text, 0, result, 6, text.Length);
        Buffer.BlockCopy(jpeg, 2, result, 6 + text.Length, jpeg.Length - 2);
        return result;
    }
}
