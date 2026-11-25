using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EeccStudio.Core.Network;
using Xunit;

namespace EeccStudio.Core.Tests;

public class WebRtcSignalingTests
{
    [Fact]
    public async Task WebRtcSignaling_OfferAnswerAndIceCandidates_ExchangedSuccessfully()
    {
        using var server = new CameraServer(0);
        server.Start();

        var ticket = server.IssuePairingTicket("EECC Studio Test", "127.0.0.1");

        // Captures d'événements serveur
        var offerTcs = new TaskCompletionSource<(CameraClient client, string sdp, string codec, int fps, string res)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var iceCandidateTcs = new TaskCompletionSource<(CameraClient client, string candidate, string? sdpMid, int sdpMLineIndex)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var capabilitiesTcs = new TaskCompletionSource<(CameraClient client, List<string> codecs, int maxFps, string maxRes)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyTcs = new TaskCompletionSource<(CameraClient client, bool ready)>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.OnWebRtcOfferReceived += (client, sdp, codec, fps, res) => offerTcs.TrySetResult((client, sdp, codec, fps, res));
        server.OnIceCandidateReceived += (client, candidate, sdpMid, sdpMLineIndex) => iceCandidateTcs.TrySetResult((client, candidate, sdpMid, sdpMLineIndex));
        server.OnWebRtcCapabilitiesReceived += (client, codecs, maxFps, maxRes) => capabilitiesTcs.TrySetResult((client, codecs, maxFps, maxRes));
        server.OnWebRtcReadyReceived += (client, ready) => readyTcs.TrySetResult((client, ready));

        using var socket = new ClientWebSocket();
        var uri = new Uri($"ws://127.0.0.1:{server.ListeningPort}/camera/control");
        await socket.ConnectAsync(uri, CancellationToken.None);

        // 1. Handshake d'appairage initial (protocole standard préservé)
        var handshake = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "handshake",
            ["token"] = ticket.Token,
            ["device_model"] = "Android 16K Camera Test",
            ["display_name"] = "Caméra Mobile 16K",
            ["reconnect"] = false,
        });
        await SendTextAsync(socket, handshake);

        var ackJson = await ReceiveTextAsync(socket);
        var ack = JsonDocument.Parse(ackJson).RootElement;
        Assert.Equal("handshake_ack", ack.GetProperty("type").GetString());
        Assert.Equal("Cam-1", ack.GetProperty("assigned_slot").GetString());

        // 2. Annonce des capacités matérielles (300 FPS & 16K HD)
        var capabilities = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "webrtc_capabilities",
            ["codecs"] = new[] { "H264", "HEVC" },
            ["max_fps"] = 300,
            ["max_resolution"] = "16K UHD (15360x8640)"
        });
        await SendTextAsync(socket, capabilities);
        var capResult = await capabilitiesTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(300, capResult.maxFps);
        Assert.Equal("16K UHD (15360x8640)", capResult.maxRes);
        Assert.Contains("H264", capResult.codecs);

        // 3. Émission de l'offre SDP WebRTC par le smartphone
        const string mockClientSdp = "v=0\r\no=camera 1234 5678 IN IP4 127.0.0.1\r\ns=EECC HighSpeed 300FPS\r\nm=video 9 UDP/TLS/RTP/SAVPF 96\r\na=rtpmap:96 H264/90000";
        var offer = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "sdp_offer",
            ["sdp"] = mockClientSdp,
            ["codec"] = "H264",
            ["fps"] = 300,
            ["resolution"] = "16K UHD"
        });
        await SendTextAsync(socket, offer);
        var offerResult = await offerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("H264", offerResult.codec);
        Assert.Equal(300, offerResult.fps);
        Assert.Equal("16K UHD", offerResult.res);
        Assert.Equal(mockClientSdp, offerResult.sdp);

        // 4. Studio répond avec une SDP Answer
        const string mockServerSdp = "v=0\r\no=studio 9876 5432 IN IP4 127.0.0.1\r\ns=EECC Studio Receiver\r\nm=video 9 UDP/TLS/RTP/SAVPF 96\r\na=rtpmap:96 H264/90000";
        await server.SendWebRtcAnswerAsync("Cam-1", mockServerSdp);

        var answerJson = await ReceiveTextAsync(socket);
        var answerDoc = JsonDocument.Parse(answerJson).RootElement;
        Assert.Equal("sdp_answer", answerDoc.GetProperty("type").GetString());
        Assert.Equal(mockServerSdp, answerDoc.GetProperty("sdp").GetString());
        Assert.Equal("accepted", answerDoc.GetProperty("status").GetString());

        // 5. Échange de candidat ICE smartphone -> Studio
        var iceMsg = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "ice_candidate",
            ["candidate"] = "candidate:1 1 UDP 2130706431 127.0.0.1 50000 typ host",
            ["sdpMid"] = "video",
            ["sdpMLineIndex"] = 0
        });
        await SendTextAsync(socket, iceMsg);
        var iceResult = await iceCandidateTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("candidate:1 1 UDP 2130706431 127.0.0.1 50000 typ host", iceResult.candidate);
        Assert.Equal("video", iceResult.sdpMid);
        Assert.Equal(0, iceResult.sdpMLineIndex);

        // 6. Échange de candidat ICE Studio -> smartphone
        await server.SendIceCandidateAsync("Cam-1", "candidate:2 1 UDP 2130706431 127.0.0.1 50002 typ host", "video", 0);
        var serverIceJson = await ReceiveTextAsync(socket);
        var serverIceDoc = JsonDocument.Parse(serverIceJson).RootElement;
        Assert.Equal("ice_candidate", serverIceDoc.GetProperty("type").GetString());
        Assert.Equal("candidate:2 1 UDP 2130706431 127.0.0.1 50002 typ host", serverIceDoc.GetProperty("candidate").GetString());

        // 7. Notification WebRTC Ready
        var readyMsg = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "webrtc_ready",
            ["ready"] = true
        });
        await SendTextAsync(socket, readyMsg);
        var readyResult = await readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(readyResult.ready);

        // 8. Rétrocompatibilité : l'envoi d'une image JPEG binaire reste totalement fonctionnel
        var frameDeliveredTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.OnCameraFrameReceived += (_, bytes) => frameDeliveredTcs.TrySetResult(bytes);

        var testJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x16, 0x00, 0x30, 0x00 };
        await socket.SendAsync(testJpeg, WebSocketMessageType.Binary, true, CancellationToken.None);
        var receivedFrame = await frameDeliveredTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(testJpeg, receivedFrame);
    }

    private static Task SendTextAsync(ClientWebSocket socket, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new InvalidOperationException("Socket closed");
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(message.ToArray());
    }
}
