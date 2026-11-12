using System.Net;
using EeccStudio.Core.Media;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Vpx.Net;

namespace EeccStudio.Core.Tests;

public class SyntheticMediaTests
{
    public const string SourceLabel = "SYNTHETIC_TEST_SOURCE";

    [Fact]
    public void Vp8_and_pcmu_roundtrip_synthetic_samples()
    {
        Assert.Equal("SYNTHETIC_TEST_SOURCE", SourceLabel);
        const int width = 64;
        const int height = 64;
        var first = Gradient(width, height, 20);
        var second = Gradient(width, height, 180);
        var codec = new VP8Codec();
        var encodedA = codec.EncodeVideo(width, height, first, VideoPixelFormatsEnum.Bgr, VideoCodecsEnum.VP8);
        var encodedB = codec.EncodeVideo(width, height, second, VideoPixelFormatsEnum.Bgr, VideoCodecsEnum.VP8);
        Assert.NotNull(encodedA);
        Assert.NotEmpty(encodedA);
        Assert.NotEqual(encodedA, encodedB);

        var decoded = codec.DecodeVideo(encodedA, VideoPixelFormatsEnum.Bgr, VideoCodecsEnum.VP8).ToList();
        Assert.NotEmpty(decoded);
        Assert.Equal((uint)width, decoded[0].Width);
        Assert.Equal((uint)height, decoded[0].Height);
        Assert.Contains(decoded[0].Sample, value => value != 0);

        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU);
        var pcm = new short[160];
        for (var i = 0; i < pcm.Length; i++) pcm[i] = (short)(Math.Sin(i / 8d) * 8000);
        var encoder = new AudioEncoder();
        var mulaw = encoder.EncodeAudio(pcm, format);
        Assert.Equal(pcm.Length, mulaw.Length);
        var back = encoder.DecodeAudio(mulaw, format);
        Assert.Equal(pcm.Length, back.Length);
        var peak = pcm.Zip(back, (input, output) => Math.Abs(input - output)).Max();
        Assert.InRange(peak, 0, 400);
    }

    [Fact]
    public async Task Synthetic_frames_cross_a_real_peer_connection()
    {
        using var answer = new LanMediaSession(IPAddress.Loopback);
        DecodedVideoFrame? video = null;
        answer.VideoFrameDecoded += frame => video = frame;
        var offerConfig = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>(),
            X_BindAddress = IPAddress.Loopback,
        };
        var offer = new RTCPeerConnection(offerConfig);
        var source = new Vp8NetVideoEncoderEndPoint();
        offer.addTrack(new MediaStreamTrack(source.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly));
        offer.addTrack(new MediaStreamTrack(new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU), MediaStreamStatusEnum.SendOnly));
        source.OnVideoSourceEncodedSample += (duration, sample) => offer.SendVideo(duration, sample);

        var offerIce = new List<RTCIceCandidateInit>();
        offer.onicecandidate += candidate =>
        {
            if (!string.IsNullOrWhiteSpace(candidate?.candidate))
            {
                answer.AddIce(candidate.candidate, candidate.sdpMid, candidate.sdpMLineIndex);
            }
        };
        answer.LocalIceCandidateJson += json =>
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var init = new RTCIceCandidateInit
            {
                candidate = root.GetProperty("candidate").GetString(),
                sdpMid = root.TryGetProperty("sdpMid", out var mid) ? mid.GetString() : null,
                sdpMLineIndex = root.TryGetProperty("sdpMLineIndex", out var line) ? line.GetUInt16() : (ushort)0,
            };
            try { offer.addIceCandidate(init); }
            catch (Exception)
            {
                offerIce.Add(init);
            }
        };

        var offerSdp = offer.createOffer(null);
        await offer.setLocalDescription(offerSdp);
        var answerSdp = await answer.AcceptOfferAsync(offerSdp.sdp!);
        Assert.Contains("VP8", answerSdp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PCMU", answerSdp, StringComparison.OrdinalIgnoreCase);
        var set = offer.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = answerSdp });
        Assert.Equal(SetDescriptionResultEnum.OK, set);
        foreach (var ice in offerIce) offer.addIceCandidate(ice);

        await source.StartVideo();
        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU);
        var pcm = new short[160];
        for (var i = 0; i < pcm.Length; i++) pcm[i] = (short)(i * 40);
        var encodedAudio = new AudioEncoder().EncodeAudio(pcm, format);
        var picture = Gradient(64, 64, 90);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && (video == null || answer.AudioPackets == 0))
        {
            source.ExternalVideoSourceRawSample(40, 64, 64, picture, VideoPixelFormatsEnum.Bgr);
            offer.SendAudio(160, encodedAudio);
            await Task.Delay(150);
        }

        Assert.True(video != null, "Aucune image VP8 décodée. ICE=" + answer.IceState + " audio=" + answer.AudioPackets);
        Assert.Equal(64, video!.Width);
        Assert.Equal(64, video.Height);
        Assert.Contains(video.Bgr, value => value != 0);
        Assert.True(answer.AudioPackets > 0, "Aucun audio PCMU décodé. ICE=" + answer.IceState);
        offer.close();
        await source.CloseVideo();
    }

    private static byte[] Gradient(int width, int height, byte offset)
    {
        var sample = new byte[width * height * 3];
        for (var i = 0; i < sample.Length; i++) sample[i] = (byte)((i + offset) % 251);
        return sample;
    }
}
