using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EeccStudio.Core.Network;
using EeccStudio.Core.Pairing;
using Xunit;
using ZXing;
using ZXing.Common;

namespace EeccStudio.Core.Tests;

/// <summary>
/// Clients WebSocket synthétiques. Ils prouvent le transport local et les règles d'appairage.
/// Ils ne remplacent pas un téléphone, une caméra ou une mesure de 15 flux.
/// </summary>
public class LanPairingTests
{
    [Fact]
    public void LanSelection_DoesNotInventAnAddress()
    {
        var selected = LanAddresses.SelectUsable(new[]
        {
            new LanCandidate("lo", "loopback", "127.0.0.1", true, true),
            new LanCandidate("vEthernet (WSL)", "Hyper-V Virtual Ethernet", "172.28.0.1", true, false),
            new LanCandidate("eth0", "Ethernet", "8.8.8.8", true, false),
            new LanCandidate("wlan0", "Wi-Fi", "169.254.4.4", true, false),
            new LanCandidate("wlan0", "Wi-Fi", "10.8.0.4", true, false),
            new LanCandidate("wlan0", "Wi-Fi", "10.8.0.4", false, false),
        });

        Assert.Equal(new[] { "10.8.0.4" }, selected);
        Assert.Empty(LanAddresses.SelectUsable(new[]
        {
            new LanCandidate("lo", "loopback", "127.0.0.1", true, true),
            new LanCandidate("eth0", "Ethernet", "1.1.1.1", true, false),
        }));

        var assigned = LanAddresses.ListAssigned();
        var present = AssignedIpv4();
        foreach (var address in assigned)
        {
            Assert.Contains(address, present);
            Assert.True(LanAddresses.IsPrivateLanIpv4(address));
        }

        using var server = new CameraServer(0);
        var detected = server.GetLocalIpAddress();
        if (detected.Length == 0)
            Assert.Empty(assigned);
        else
            Assert.Contains(detected, assigned);
    }

    [Fact]
    public void PairingTicket_IsUnpredictable_QrDecodesToThatPayload()
    {
        using var server = Start();
        var first = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
        var second = server.IssuePairingTicket("EECC Studio", "10.8.0.4");

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(22, first.Token.Length);
        Assert.Matches("^[A-Za-z0-9_-]{22}$", first.Token);
        Assert.DoesNotContain("EECC-LIVE", first.ToQrJson());
        Assert.DoesNotContain("password", first.ToQrJson(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", first.ToQrJson(), StringComparison.OrdinalIgnoreCase);

        var payload = JsonDocument.Parse(first.ToQrJson()).RootElement;
        Assert.Equal(1, payload.GetProperty("v").GetInt32());
        Assert.Equal("10.8.0.4", payload.GetProperty("studio_ip").GetString());
        Assert.Equal(server.ListeningPort, payload.GetProperty("port").GetInt32());
        Assert.Equal(first.Token, payload.GetProperty("token").GetString());
        Assert.Equal("ws-jpeg", payload.GetProperty("transport").GetString());
        Assert.True(first.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(3).AddSeconds(5));
        Assert.True(first.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2));

        var decoded = DecodeQr(PairingQr.CreateMatrix(first.ToQrJson()));
        Assert.Equal(first.ToQrJson(), decoded);

        Assert.Throws<ArgumentException>(() => server.IssuePairingTicket("EECC Studio", "8.8.8.8"));
        Assert.Throws<ArgumentException>(() => server.IssuePairingTicket("EECC Studio", "192.168.1.100.1"));
        Assert.Throws<ArgumentException>(() => server.IssuePairingTicket("EECC Studio", ""));
    }

    [Fact]
    public async Task InvalidAndRevokedTokens_AreRejected()
    {
        using var server = Start();
        var ticket = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
        Assert.True(server.RevokePairingTicket(ticket.Token));

        var revoked = await Handshake(server, ticket.ToQrJson());
        Assert.Equal("token_invalid", revoked.GetProperty("code").GetString());
        Assert.Equal(0, server.PhoneCount);

        var unknown = ticket.ToQrJson().Replace(ticket.Token, "aaaaaaaaaaaaaaaaaaaaaa", StringComparison.Ordinal);
        var rejected = await Handshake(server, unknown);
        Assert.Equal("token_invalid", rejected.GetProperty("code").GetString());
        Assert.Equal(0, server.PhoneCount);
        Assert.Equal(0, server.TotalFramesReceived);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        using var server = Start();
        var now = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        server.Clock = () => now;
        var ticket = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
        now = now.AddMinutes(4);

        var rejected = await Handshake(server, ticket.ToQrJson());
        Assert.Equal("token_expired", rejected.GetProperty("code").GetString());
        Assert.Equal(0, server.PhoneCount);
        Assert.False(ticket.Consumed);
    }

    [Fact]
    public async Task LocalTransport_DeliversFrameTelemetryAndCommand_ThenRejectsReuse()
    {
        using var server = Start();
        var ticket = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
        var frameSeen = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.OnCameraFrameReceived += (_, frame) => frameSeen.TrySetResult(frame);

        using var socket = await Connect(server.ListeningPort);
        await SendText(socket, HandshakeJson(ticket.Token, "Pupitre", reconnect: false));
        var ack = JsonDocument.Parse(await ReceiveText(socket)).RootElement;
        Assert.Equal("handshake_ack", ack.GetProperty("type").GetString());
        Assert.Equal("Cam-1", ack.GetProperty("assigned_slot").GetString());
        var sessionId = ack.GetProperty("session_id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sessionId));
        Assert.NotEqual(ticket.Token, sessionId);
        Assert.Equal(1, server.ConnectedPhoneCount);
        Assert.Equal("Pupitre", server.ConnectedCameras["Cam-1"].DisplayName);

        // Image synthétique : ce n'est pas la caméra d'un téléphone.
        var syntheticFrame = new byte[] { 0xFF, 0xD8, 0xEE, 0xC1, 0x0A, 0x01 };
        await socket.SendAsync(syntheticFrame, WebSocketMessageType.Binary, true, CancellationToken.None);
        var delivered = await frameSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(syntheticFrame, delivered);
        Assert.Equal(syntheticFrame, server.GetCameraFrame("Cam-1"));

        await SendText(socket, "{\"type\":\"telemetry\",\"payload\":{\"battery_level\":42,\"is_charging\":true,\"audio_level\":0.25,\"fps\":12,\"bitrate_kbps\":800}}");
        var telemetry = await WaitForPhoneAsync(() =>
        {
            var phones = server.ConnectedCameras;
            if (!phones.TryGetValue("Cam-1", out var phone) || phone.LastTelemetryTime == DateTimeOffset.UnixEpoch)
                return null;
            return phone.BatteryLevel == 42 && phone.IsCharging && phone.Fps == 12 && phone.BitrateKbps == 800
                ? phone
                : null;
        });
        Assert.True(telemetry.IsCharging);
        Assert.Equal(0.25, telemetry.AudioLevel, 2);
        Assert.Equal(12, telemetry.Fps);
        Assert.Equal(800, telemetry.BitrateKbps);

        await server.SendCommandAsync("Cam-1", "toggle_torch");
        var command = JsonDocument.Parse(await ReceiveText(socket)).RootElement;
        Assert.Equal("command", command.GetProperty("type").GetString());
        Assert.Equal("toggle_torch", command.GetProperty("command").GetString());

        await server.SetTallyAsync("Cam-1", TallyState.Program);
        var tally = JsonDocument.Parse(await ReceiveText(socket)).RootElement;
        Assert.Equal("program", tally.GetProperty("state").GetString());

        using var reuse = await Connect(server.ListeningPort);
        await SendText(reuse, HandshakeJson(ticket.Token, "Autre", reconnect: false));
        var used = JsonDocument.Parse(await ReceiveText(reuse)).RootElement;
        Assert.Equal("token_used", used.GetProperty("code").GetString());
        Assert.Equal(1, server.PhoneCount);
    }

    [Fact]
    public async Task Disconnect_ReconnectsWithSession_ByeReleasesTheSlot()
    {
        using var server = Start();
        server.ReconnectGrace = TimeSpan.FromSeconds(30);
        var ticket = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.OnCameraDisconnected += _ => disconnected.TrySetResult();

        var socket = await Connect(server.ListeningPort);
        await SendText(socket, HandshakeJson(ticket.Token, "Chœur", reconnect: false));
        var ack = JsonDocument.Parse(await ReceiveText(socket)).RootElement;
        var sessionId = ack.GetProperty("session_id").GetString()!;
        socket.Dispose();
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, server.ConnectedPhoneCount);
        Assert.Equal(1, server.PhoneCount);

        using var again = await Connect(server.ListeningPort);
        await SendText(again, HandshakeJson(sessionId, "Chœur", reconnect: true));
        var restored = JsonDocument.Parse(await ReceiveText(again)).RootElement;
        Assert.Equal("handshake_ack", restored.GetProperty("type").GetString());
        Assert.Equal("Cam-1", restored.GetProperty("assigned_slot").GetString());
        Assert.Equal(1, server.ConnectedPhoneCount);

        var gone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.OnCameraDisconnected += _ => gone.TrySetResult();
        await SendText(again, """{"type":"bye"}""");
        await gone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, server.PhoneCount);

        using var late = await Connect(server.ListeningPort);
        await SendText(late, HandshakeJson(sessionId, "Chœur", reconnect: true));
        var rejected = JsonDocument.Parse(await ReceiveText(late)).RootElement;
        Assert.Equal("session_unknown", rejected.GetProperty("code").GetString());
    }

    [Fact]
    public async Task FifteenPhones_AreAccepted_TheSixteenthIsRefused_PhysicalCamerasAreSeparate()
    {
        using var server = Start();
        var now = DateTimeOffset.UtcNow;
        server.Clock = () => now;
        server.ReconnectGrace = TimeSpan.FromSeconds(30);
        for (var index = 1; index <= 20; index++)
            server.RegisterPhysicalCamera($"Cam-{index}", $"USB {index}");
        server.RegisterPhysicalCamera("sdi-1", "Carte d'acquisition");
        Assert.Equal(21, server.PhysicalCameraCount);

        var sockets = new List<ClientWebSocket>();
        try
        {
            var connections = Enumerable.Range(1, 15).Select(async index =>
            {
                var ticket = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
                var socket = await Connect(server.ListeningPort);
                await SendText(socket, HandshakeJson(ticket.Token, $"Téléphone {index}", reconnect: false));
                var ack = JsonDocument.Parse(await ReceiveText(socket)).RootElement;
                Assert.Equal("handshake_ack", ack.GetProperty("type").GetString());
                var slot = ack.GetProperty("assigned_slot").GetString()!;
                var marker = new byte[] { 0xFF, 0xD8, (byte)index };
                await socket.SendAsync(marker, WebSocketMessageType.Binary, true, CancellationToken.None);
                return (socket, slot, marker, name: $"Téléphone {index}");
            });
            var phones = await Task.WhenAll(connections);
            sockets.AddRange(phones.Select(phone => phone.socket));

            Assert.Equal(15, phones.Select(phone => phone.slot).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(15, server.ConnectedPhoneCount);
            Assert.Equal(21, server.PhysicalCameraCount);
            await WaitUntilAsync(() => server.TotalFramesReceived >= 15);
            foreach (var phone in phones)
            {
                Assert.Equal(phone.name, server.ConnectedCameras[phone.slot].DisplayName);
                Assert.Equal(phone.marker, server.GetCameraFrame(phone.slot));
            }

            var extra = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
            var refused = await Handshake(server, extra.ToQrJson());
            Assert.Equal("capacity", refused.GetProperty("code").GetString());
            Assert.Contains("15 téléphones", refused.GetProperty("message").GetString());
            Assert.Equal(15, server.PhoneCount);
            Assert.False(extra.Consumed);

            foreach (var socket in sockets)
                socket.Dispose();
            sockets.Clear();
            await WaitUntilAsync(() => server.ConnectedPhoneCount == 0);
            Assert.Equal(15, server.PhoneCount);

            var duringGrace = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
            var stillFull = await Handshake(server, duringGrace.ToQrJson());
            Assert.Equal("capacity", stillFull.GetProperty("code").GetString());

            now = now.AddMinutes(2);
            Assert.Equal(0, server.PhoneCount);
            var afterGrace = server.IssuePairingTicket("EECC Studio", "10.8.0.4");
            var admitted = await Handshake(server, afterGrace.ToQrJson());
            Assert.Equal("handshake_ack", admitted.GetProperty("type").GetString());
            Assert.Equal(1, server.PhoneCount);
        }
        finally
        {
            foreach (var socket in sockets)
                socket.Dispose();
        }
    }

    [Fact]
    public void SecondListener_OnTheSamePort_ReportsTheFailure()
    {
        using var first = Start();
        using var second = new CameraServer(first.ListeningPort);
        var started = second.TryStart(out var detail);
        Assert.False(started);
        Assert.Contains("n'a pas pu être ouvert", detail);
        Assert.Equal(detail, second.StartError);
    }

    private static CameraServer Start()
    {
        var server = new CameraServer(0);
        Assert.True(server.TryStart(out var detail), detail);
        return server;
    }

    private static async Task<JsonElement> Handshake(CameraServer server, string qrJson)
    {
        var token = JsonDocument.Parse(qrJson).RootElement.GetProperty("token").GetString()!;
        using var socket = await Connect(server.ListeningPort);
        await SendText(socket, HandshakeJson(token, "Essai", reconnect: false));
        return JsonDocument.Parse(await ReceiveText(socket)).RootElement.Clone();
    }

    private static string HandshakeJson(string token, string displayName, bool reconnect) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "handshake",
            ["role"] = "camera_source",
            ["token"] = token,
            ["reconnect"] = reconnect,
            ["display_name"] = displayName,
            ["device_model"] = "SYNTHETIC_TEST_CLIENT",
            ["device_id"] = "synthetic-" + displayName,
        });

    private static async Task<ClientWebSocket> Connect(int port)
    {
        var socket = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/camera/control"), timeout.Token);
        return socket;
    }

    private static Task SendText(ClientWebSocket socket, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

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
                throw new InvalidOperationException("Le serveur a fermé le socket sans message.");
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

        throw new TimeoutException("La condition de transport n'a pas été atteinte.");
    }

    private static async Task<CameraClient> WaitForPhoneAsync(Func<CameraClient?> read)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var value = read();
            if (value != null)
                return value;
            await Task.Delay(20);
        }

        throw new TimeoutException("La télémétrie n'est pas arrivée.");
    }

    private static string DecodeQr(bool[,] modules)
    {
        const int scale = 8;
        const int quiet = 4;
        var modulesPerSide = modules.GetLength(0);
        var size = (modulesPerSide + (quiet * 2)) * scale;
        var rgb = new byte[size * size * 3];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var moduleX = (x / scale) - quiet;
                var moduleY = (y / scale) - quiet;
                var dark = moduleX >= 0 && moduleY >= 0 && moduleX < modulesPerSide && moduleY < modulesPerSide && modules[moduleX, moduleY];
                var pixel = ((y * size) + x) * 3;
                var value = dark ? (byte)0 : (byte)255;
                rgb[pixel] = value;
                rgb[pixel + 1] = value;
                rgb[pixel + 2] = value;
            }
        }

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
            },
        };
        var source = new RGBLuminanceSource(rgb, size, size, RGBLuminanceSource.BitmapFormat.RGB24);
        var result = reader.Decode(source);
        Assert.NotNull(result);
        return result!.Text;
    }

    private static HashSet<string> AssignedIpv4()
    {
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    addresses.Add(unicast.Address.ToString());
            }
        }

        return addresses;
    }
}
