using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EeccStudio.Core.Network;
using EeccStudio.Core.Pairing;
using Xunit;
using ZXing;
using ZXing.Common;

namespace EeccStudio.Core.Tests;

public class QrRotationTests
{
    [Fact]
    public async Task ShowingAQrDoesNotConsumeIt_ValidationRotatesTheImage()
    {
        using var server = Start();
        server.RegisterPhysicalCamera("usb-1", "Caméra USB");
        var board = new PairingBoard();
        var image = new ShownQr();

        var first = board.Refresh(server, "10.8.0.4");
        Assert.True(image.Apply(first.QrJson));
        Assert.Equal(1, image.Changes);

        using (var idle = await Connect(server.ListeningPort))
        {
            await SendText(idle, """{"type":"ping"}""");
            await Task.Delay(100);
        }

        var unchanged = board.Refresh(server, "10.8.0.4");
        Assert.False(image.Apply(unchanged.QrJson));
        Assert.Equal(first.QrJson, image.Json);

        using var phone = await Connect(server.ListeningPort);
        await SendText(phone, HandshakeJson(TokenOf(first.QrJson!), "Pupitre"));
        var ack = JsonDocument.Parse(await ReceiveText(phone)).RootElement;
        Assert.Equal("handshake_ack", ack.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(ack.GetProperty("session_id").GetString()));

        var second = board.Refresh(server, "10.8.0.4");
        Assert.NotEqual(first.QrJson, second.QrJson);
        Assert.True(image.Apply(second.QrJson));
        Assert.Equal(2, image.Changes);
        Assert.Equal(second.QrJson, image.Json);

        var reused = await Handshake(server, first.QrJson!);
        Assert.Equal("token_used", reused.GetProperty("code").GetString());
        Assert.Equal(1, server.PhoneCount);
        Assert.Equal(1, server.PhysicalCameraCount);

        await SendText(phone, """{"type":"telemetry","payload":{"battery_level":41,"is_charging":false,"audio_level":0,"fps":1,"bitrate_kbps":1}}""");
        await WaitUntilAsync(() => server.ConnectedCameras.Values.Any(item => item.BatteryLevel == 41));
    }

    [Fact]
    public async Task SameQrAcceptedOnceWhenTwoHandshakesRace()
    {
        using var server = Start();
        var board = new PairingBoard();
        var qr = board.Refresh(server, "10.8.0.4").QrJson!;
        var token = TokenOf(qr);
        using var first = await Connect(server.ListeningPort);
        using var second = await Connect(server.ListeningPort);
        var replies = await Task.WhenAll(
            Exchange(first, token, "A"),
            Exchange(second, token, "B"));
        var accepted = replies.Count(reply => reply.GetProperty("type").GetString() == "handshake_ack");
        var rejected = replies.Count(reply => reply.TryGetProperty("code", out var code) && code.GetString() == "token_used");
        Assert.Equal(1, accepted);
        Assert.Equal(1, rejected);
        Assert.Equal(1, server.ConnectedPhoneCount);

        var image = new ShownQr();
        Assert.True(image.Apply(qr));
        var next = board.Refresh(server, "10.8.0.4");
        Assert.True(image.Apply(next.QrJson));
        Assert.NotEqual(qr, image.Json);
        Assert.Equal(1, server.PhoneCount);
    }

    [Fact]
    public async Task FifteenValidatedPhonesThenTheWindowStopsShowingAQr()
    {
        using var server = Start();
        for (var index = 0; index < 12; index++)
            server.RegisterPhysicalCamera($"phys-{index}", $"Physique {index}");
        var board = new PairingBoard();
        var image = new ShownQr();
        var display = board.Refresh(server, "10.8.0.4");
        var sockets = new List<ClientWebSocket>();
        try
        {
            for (var index = 1; index <= 15; index++)
            {
                Assert.True(display.ShowsQr);
                Assert.NotNull(display.QrJson);
                image.Apply(display.QrJson);
                var socket = await Connect(server.ListeningPort);
                sockets.Add(socket);
                await SendText(socket, HandshakeJson(TokenOf(display.QrJson!), $"Téléphone {index}"));
                var ack = JsonDocument.Parse(await ReceiveText(socket)).RootElement;
                Assert.Equal("handshake_ack", ack.GetProperty("type").GetString());
                var previous = display.QrJson;
                display = board.Refresh(server, "10.8.0.4");
                var again = await Handshake(server, previous!);
                Assert.Equal("token_used", again.GetProperty("code").GetString());
                Assert.Equal(index, server.PhoneCount);
            }

            Assert.False(display.ShowsQr);
            Assert.Null(display.QrJson);
            Assert.Equal(PairingBoard.CapacityMessage, display.Message);
            Assert.True(image.Apply(null));
            Assert.Null(image.Json);
            Assert.Equal(12, server.PhysicalCameraCount);
            Assert.Equal(15, server.PhoneCount);
        }
        finally
        {
            foreach (var socket in sockets)
                socket.Dispose();
        }
    }

    [Fact]
    public void StudioPngDecodesToTheTicketJson()
    {
        using var server = Start();
        var json = server.IssuePairingTicket("EECC Studio", "10.8.0.4").ToQrJson();
        var png = PairingQr.ToPng(json);
        var pixels = ReadGray(png);
        Assert.All(pixels.Gray, value => Assert.True(value is 0 or 255));
        Assert.Equal(255, pixels.Gray[0]);
        var decoded = Decode(pixels);
        Assert.Equal(json, decoded);
        using var document = JsonDocument.Parse(decoded);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("v").GetInt32());
        Assert.Equal("ws-jpeg", root.GetProperty("transport").GetString());
        Assert.Equal("/camera/control", root.GetProperty("path").GetString());
        Assert.Equal(22, root.GetProperty("token").GetString()!.Length);
        Assert.DoesNotContain("demo", root.GetProperty("token").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.False(root.GetProperty("token").GetString()!.StartsWith("sess_", StringComparison.Ordinal));
    }

    private static CameraServer Start()
    {
        var server = new CameraServer(0);
        Assert.True(server.TryStart(out var detail), detail);
        return server;
    }

    private static async Task<JsonElement> Handshake(CameraServer server, string qrJson)
    {
        using var socket = await Connect(server.ListeningPort);
        await SendText(socket, HandshakeJson(TokenOf(qrJson), "Essai"));
        return JsonDocument.Parse(await ReceiveText(socket)).RootElement.Clone();
    }

    private static async Task<JsonElement> Exchange(ClientWebSocket socket, string token, string name)
    {
        await SendText(socket, HandshakeJson(token, name));
        return JsonDocument.Parse(await ReceiveText(socket)).RootElement.Clone();
    }

    private static string TokenOf(string qrJson) =>
        JsonDocument.Parse(qrJson).RootElement.GetProperty("token").GetString()!;

    private static string HandshakeJson(string token, string displayName) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "handshake",
            ["role"] = "camera_source",
            ["token"] = token,
            ["reconnect"] = false,
            ["display_name"] = displayName,
            ["device_model"] = "SYNTHETIC_TEST_CLIENT",
        });

    private static async Task<ClientWebSocket> Connect(int port)
    {
        var socket = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/camera/control"), timeout.Token);
        return socket;
    }

    private static Task SendText(ClientWebSocket socket, string json) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<string> ReceiveText(ClientWebSocket socket)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new InvalidOperationException("Fermeture sans message.");
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(message.ToArray());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(20);
        }

        throw new TimeoutException("condition non atteinte");
    }

    private static (byte[] Gray, int Width, int Height) ReadGray(byte[] png)
    {
        var offset = 8;
        byte[]? inflated = null;
        var width = 0;
        var height = 0;
        while (offset + 8 <= png.Length)
        {
            var length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "IHDR")
            {
                width = (png[offset + 8] << 24) | (png[offset + 9] << 16) | (png[offset + 10] << 8) | png[offset + 11];
                height = (png[offset + 12] << 24) | (png[offset + 13] << 16) | (png[offset + 14] << 8) | png[offset + 15];
            }
            else if (type == "IDAT")
            {
                using var input = new MemoryStream(png, offset + 8, length);
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

        var gray = new byte[width * height];
        var cursor = 0;
        for (var y = 0; y < height; y++)
        {
            cursor++;
            Buffer.BlockCopy(inflated!, cursor, gray, y * width, width);
            cursor += width;
        }

        return (gray, width, height);
    }

    private static string Decode((byte[] Gray, int Width, int Height) pixels)
    {
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
        return reader.Decode(source).Text;
    }
}
