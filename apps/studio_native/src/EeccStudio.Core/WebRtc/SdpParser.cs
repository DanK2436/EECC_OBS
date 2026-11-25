using System;
using System.Collections.Generic;
using System.Text;

namespace EeccStudio.Core.WebRtc
{
    /// <summary>
    /// Analyseur et générateur de descriptions de session SDP (Session Description Protocol)
    /// pour la négociation WebRTC entre EECC Caméra et EECC Studio (RFC 4566, RFC 8866).
    /// </summary>
    public static class SdpParser
    {
        public static string GenerateAnswer(string offerSdp, string localIp = "127.0.0.1", int rtpPort = 50000)
        {
            var lines = offerSdp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();

            sb.Append("v=0\r\n");
            sb.Append($"o=EECCStudio {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} 2 IN IP4 {localIp}\r\n");
            sb.Append("s=EECC Studio WebRTC Receiver\r\n");
            sb.Append($"c=IN IP4 {localIp}\r\n");
            sb.Append("t=0 0\r\n");
            sb.Append("a=group:BUNDLE audio video\r\n");
            sb.Append("a=msid-semantic: WMS\r\n");

            bool inAudio = false;
            bool inVideo = false;
            int audioPayloadType = 111; // Opus par défaut
            int videoPayloadType = 96;  // H.264 par défaut

            foreach (var line in lines)
            {
                if (line.StartsWith("m=audio"))
                {
                    inAudio = true;
                    inVideo = false;
                    var parts = line.Split(' ');
                    if (parts.Length > 3 && int.TryParse(parts[3], out var pt))
                        audioPayloadType = pt;
                }
                else if (line.StartsWith("m=video"))
                {
                    inVideo = true;
                    inAudio = false;
                    var parts = line.Split(' ');
                    if (parts.Length > 3 && int.TryParse(parts[3], out var pt))
                        videoPayloadType = pt;
                }
            }

            // Description média Audio (Opus 48kHz stéréo pour mixage dans AudioEngine)
            sb.Append($"m=audio {rtpPort} UDP/TLS/RTP/SAVPF {audioPayloadType}\r\n");
            sb.Append($"c=IN IP4 {localIp}\r\n");
            sb.Append($"a=rtpmap:{audioPayloadType} opus/48000/2\r\n");
            sb.Append($"a=fmtp:{audioPayloadType} minptime=10;useinbandfec=1\r\n");
            sb.Append("a=mid:audio\r\n");
            sb.Append("a=recvonly\r\n");

            // Description média Vidéo (H.264 matériel MediaCodec)
            sb.Append($"m=video {rtpPort} UDP/TLS/RTP/SAVPF {videoPayloadType}\r\n");
            sb.Append($"c=IN IP4 {localIp}\r\n");
            sb.Append($"a=rtpmap:{videoPayloadType} H264/90000\r\n");
            sb.Append($"a=fmtp:{videoPayloadType} level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=42e01f\r\n");
            sb.Append("a=mid:video\r\n");
            sb.Append("a=recvonly\r\n");
            sb.Append("a=rtcp-mux\r\n");
            sb.Append("a=b=AS:50000\r\n"); // Support bande passante jusqu'à 50 Mbps pour 16K/4K

            return sb.ToString();
        }

        public static (int videoPt, int audioPt, string videoCodec) ParseOfferPayloads(string offerSdp)
        {
            int videoPt = 96;
            int audioPt = 111;
            string videoCodec = "H264";

            var lines = offerSdp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("a=rtpmap:"))
                {
                    var map = line.Substring("a=rtpmap:".Length);
                    var parts = map.Split(' ');
                    if (parts.Length >= 2)
                    {
                        if (parts[1].StartsWith("H264", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(parts[0], out var pt)) videoPt = pt;
                            videoCodec = "H264";
                        }
                        else if (parts[1].StartsWith("H265", StringComparison.OrdinalIgnoreCase) || parts[1].StartsWith("HEVC", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(parts[0], out var pt)) videoPt = pt;
                            videoCodec = "HEVC";
                        }
                        else if (parts[1].StartsWith("opus", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(parts[0], out var pt)) audioPt = pt;
                        }
                    }
                }
            }

            return (videoPt, audioPt, videoCodec);
        }
    }
}
