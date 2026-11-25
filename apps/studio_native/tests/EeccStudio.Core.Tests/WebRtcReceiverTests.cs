using System;
using System.Text;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Network;
using EeccStudio.Core.WebRtc;
using Xunit;

namespace EeccStudio.Core.Tests;

public class WebRtcReceiverTests
{
    [Fact]
    public void SdpParser_GeneratesCompliantH264AndOpusAnswer()
    {
        const string mockOffer =
            "v=0\r\n" +
            "o=- 12345 2 IN IP4 192.168.1.50\r\n" +
            "s=EECC Camera\r\n" +
            "t=0 0\r\n" +
            "m=audio 9 UDP/TLS/RTP/SAVPF 111\r\n" +
            "a=rtpmap:111 opus/48000/2\r\n" +
            "m=video 9 UDP/TLS/RTP/SAVPF 96\r\n" +
            "a=rtpmap:96 H264/90000\r\n" +
            "a=fmtp:96 packetization-mode=1\r\n";

        var answer = SdpParser.GenerateAnswer(mockOffer, "192.168.1.100", 50002);

        Assert.Contains("v=0", answer);
        Assert.Contains("o=EECCStudio", answer);
        Assert.Contains("m=audio 50002 UDP/TLS/RTP/SAVPF 111", answer);
        Assert.Contains("a=rtpmap:111 opus/48000/2", answer);
        Assert.Contains("m=video 50002 UDP/TLS/RTP/SAVPF 96", answer);
        Assert.Contains("a=rtpmap:96 H264/90000", answer);
        Assert.Contains("a=b=AS:50000", answer); // Bande passante haute fidélité 16K/300 FPS

        var (videoPt, audioPt, codec) = SdpParser.ParseOfferPayloads(mockOffer);
        Assert.Equal(96, videoPt);
        Assert.Equal(111, audioPt);
        Assert.Equal("H264", codec);
    }

    [Fact]
    public void RtpPacket_ParsesHeaderAndPayloadCorrectly()
    {
        // Construction d'un paquet RTP synthétique (RFC 3550)
        // V=2, P=0, X=0, CC=0 (0x80), M=1, PT=96 (0xE0)
        // Seq=1001 (0x03, 0xE9), Timestamp=90000 (0x00, 0x01, 0x5F, 0x90), SSRC=123456 (0x00, 0x01, 0xE2, 0x40)
        var rawPacket = new byte[]
        {
            0x80, 0xE0, 0x03, 0xE9,
            0x00, 0x01, 0x5F, 0x90,
            0x00, 0x01, 0xE2, 0x40,
            // NALU H.264 NAL type 7 (SPS)
            0x67, 0x42, 0x00, 0x1F, 0xE9
        };

        var parsed = RtpPacket.TryParse(rawPacket, out var packet);
        Assert.True(parsed);
        Assert.NotNull(packet);
        Assert.Equal(2, packet!.Version);
        Assert.False(packet.HasPadding);
        Assert.False(packet.HasExtension);
        Assert.True(packet.Marker);
        Assert.Equal(96, packet.PayloadType);
        Assert.Equal(1001, packet.SequenceNumber);
        Assert.Equal(90000u, packet.Timestamp);
        Assert.Equal(123456u, packet.Ssrc);
        Assert.Equal(5, packet.Payload.Length);
        Assert.Equal(0x67, packet.Payload.Span[0]);
    }

    [Fact]
    public async Task WebRtcCameraReceiver_AssemblesH264VideoAndRoutesOpusAudioToAudioEngine()
    {
        using var server = new CameraServer(0);
        server.Start();
        var audioEngine = new AudioEngine();

        var client = new CameraClient
        {
            StreamName = "Cam-1",
            SlotNumber = 1,
            DisplayName = "Smartphone Test 16K"
        };

        using var receiver = new WebRtcCameraReceiver(client, server, audioEngine);
        Assert.Equal(WebRtcReceiverState.Idle, receiver.State);

        // 1. Négociation de l'offre
        const string mockOffer =
            "v=0\r\nm=audio 9 UDP/TLS/RTP/SAVPF 111\r\na=rtpmap:111 opus/48000/2\r\n" +
            "m=video 9 UDP/TLS/RTP/SAVPF 96\r\na=rtpmap:96 H264/90000\r\n";

        var answer = await receiver.ProcessOfferAsync(mockOffer, codec: "H264", fps: 300, resolution: "16K UHD");
        Assert.Equal(WebRtcReceiverState.Connected, receiver.State);
        Assert.Contains("m=video", answer);

        // 2. Réception d'un paquet vidéo H.264
        byte[]? receivedVideoFrame = null;
        receiver.OnVideoFrameAvailable += (slot, frame) => receivedVideoFrame = frame;

        // Paquet RTP vidéo avec Marker=true
        var rtpVideo = new byte[]
        {
            0x80, 0xE0, 0x00, 0x01,
            0x00, 0x00, 0x03, 0xE8,
            0x00, 0x00, 0x00, 0x01,
            0x65, 0x88, 0x84, 0x00 // NAL type 5 (IDR Slice)
        };
        receiver.IngestRtpPacket(rtpVideo);

        Assert.NotNull(receivedVideoFrame);
        Assert.Equal(1, receiver.FramesReceivedCount);
        // Doit contenir l'annexe B start code 0x00000001 + NALU
        Assert.Equal(0x00, receivedVideoFrame![0]);
        Assert.Equal(0x00, receivedVideoFrame[1]);
        Assert.Equal(0x00, receivedVideoFrame[2]);
        Assert.Equal(0x01, receivedVideoFrame[3]);
        Assert.Equal(0x65, receivedVideoFrame[4]);

        // 3. Réception d'un paquet audio Opus (PayloadType = 111)
        byte[]? receivedAudioData = null;
        receiver.OnAudioFrameAvailable += (slot, data) => receivedAudioData = data;

        var rtpAudio = new byte[]
        {
            0x80, 0x6F, 0x00, 0x02, // PT=111 (0x6F)
            0x00, 0x00, 0x07, 0xD0,
            0x00, 0x00, 0x00, 0x02,
            0x03, 0x00, 0x04, 0x00 // Échantillons audio Opus/PCM
        };
        receiver.IngestRtpPacket(rtpAudio);

        Assert.NotNull(receivedAudioData);
        Assert.Equal(1, receiver.AudioPacketsReceivedCount);

        // 4. Vérification que le mixeur AudioEngine a bien le canal mobile
        var channels = audioEngine.Channels;
        var mobileChannel = Assert.Single(channels, c => c.Id == "mobile_cam_1");
        Assert.Equal("Audio Caméra Mobile 1", mobileChannel.Name);

        // Rendu audio du master pour confirmer que les échantillons transitent sans exception
        var masterPcm = audioEngine.RenderMaster(128);
        Assert.NotNull(masterPcm);
        Assert.True(masterPcm.Length > 0);
    }

    [Fact]
    public void WebRtcSessionManager_CoordinatesReceiversAcrossMultipleSlots()
    {
        using var server = new CameraServer(0);
        server.Start();
        var audioEngine = new AudioEngine();

        using var sessionManager = new WebRtcSessionManager(server, audioEngine);

        var client1 = new CameraClient { StreamName = "Cam-1", SlotNumber = 1 };
        var client2 = new CameraClient { StreamName = "Cam-2", SlotNumber = 2 };

        var rec1 = sessionManager.GetOrCreateReceiver(client1);
        var rec2 = sessionManager.GetOrCreateReceiver(client2);

        Assert.NotNull(rec1);
        Assert.NotNull(rec2);
        Assert.Equal("Cam-1", rec1.StreamName);
        Assert.Equal("Cam-2", rec2.StreamName);
        Assert.Equal(2, sessionManager.ActiveReceivers.Count);

        Assert.Same(rec1, sessionManager.GetReceiver("Cam-1"));
        Assert.Same(rec2, sessionManager.GetReceiver("Cam-2"));
    }
}
