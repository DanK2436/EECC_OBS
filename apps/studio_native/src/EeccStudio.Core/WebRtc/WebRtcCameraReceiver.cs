using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Network;

namespace EeccStudio.Core.WebRtc
{
    public enum WebRtcReceiverState
    {
        Idle,
        Negotiating,
        Connected,
        Closed
    }

    /// <summary>
    /// Récepteur de session WebRTC pour une caméra mobile EECC connectée (Cam-1 à Cam-15).
    /// Gère la négociation SDP, l'assemblage des paquets RTP H.264 et le routage Opus vers le mixeur audio.
    /// </summary>
    public sealed class WebRtcCameraReceiver : IDisposable
    {
        private readonly CameraClient _client;
        private readonly CameraServer _server;
        private readonly AudioEngine? _audioEngine;
        private readonly object _lock = new();

        public string StreamName => _client.StreamName;
        public int SlotNumber => _client.SlotNumber;
        public WebRtcReceiverState State { get; private set; } = WebRtcReceiverState.Idle;
        public string ActiveVideoCodec { get; private set; } = "H264";
        public int TargetFps { get; private set; } = 300;
        public string Resolution { get; private set; } = "16K UHD";

        // Métriques de transport
        public int FramesReceivedCount { get; private set; }
        public int AudioPacketsReceivedCount { get; private set; }
        public DateTimeOffset ConnectedAt { get; private set; } = DateTimeOffset.UtcNow;

        // Événements
        public event Action<string, byte[]>? OnVideoFrameAvailable;
        public event Action<string, byte[]>? OnAudioFrameAvailable;

        // Reconstitution des NALUs H.264 depuis les paquets RTP
        private readonly MemoryStream _naluAssemblyStream = new();

        public WebRtcCameraReceiver(CameraClient client, CameraServer server, AudioEngine? audioEngine = null)
        {
            _client = client;
            _server = server;
            _audioEngine = audioEngine;
            if (_audioEngine != null && _client.SlotNumber > 0)
            {
                _audioEngine.AddChannel($"mobile_cam_{_client.SlotNumber}", $"Audio Caméra Mobile {_client.SlotNumber}");
            }
        }

        /// <summary>
        /// Traite l'offre SDP reçue du smartphone et génère la réponse SDP compatible H.264 / Opus
        /// </summary>
        public async Task<string> ProcessOfferAsync(string offerSdp, string codec = "H264", int fps = 300, string resolution = "16K UHD")
        {
            lock (_lock)
            {
                State = WebRtcReceiverState.Negotiating;
                ActiveVideoCodec = codec;
                TargetFps = fps;
                Resolution = resolution;
                _client.Resolution = resolution;
                _client.Fps = fps;
            }

            var localIp = _server.GetLocalIpAddress();
            if (string.IsNullOrEmpty(localIp)) localIp = "127.0.0.1";

            // Génération de la réponse SDP compatible (H.264 96 + Opus 111)
            var answerSdp = SdpParser.GenerateAnswer(offerSdp, localIp, 50000 + (SlotNumber * 2));

            // Envoi de la réponse au smartphone via le WebSocket de signalisation
            await _server.SendWebRtcAnswerAsync(StreamName, answerSdp);

            // Envoi d'un candidat ICE local
            await _server.SendIceCandidateAsync(
                StreamName,
                $"candidate:1 1 UDP 2130706431 {localIp} {50000 + (SlotNumber * 2)} typ host",
                "video",
                0
            );

            lock (_lock)
            {
                State = WebRtcReceiverState.Connected;
            }

            return answerSdp;
        }

        /// <summary>
        /// Reçoit et dépaquète un paquet RTP entrant (H.264 ou Opus)
        /// </summary>
        public void IngestRtpPacket(ReadOnlyMemory<byte> rawRtp)
        {
            if (!RtpPacket.TryParse(rawRtp, out var packet) || packet == null)
                return;

            if (packet.PayloadType == 96 || packet.PayloadType == 98) // Vidéo H.264
            {
                HandleVideoRtp(packet);
            }
            else if (packet.PayloadType == 111 || packet.PayloadType == 97) // Audio Opus
            {
                HandleAudioRtp(packet);
            }
        }

        private void HandleVideoRtp(RtpPacket packet)
        {
            lock (_lock)
            {
                FramesReceivedCount++;
                var span = packet.Payload.Span;
                if (span.IsEmpty) return;

                // Reconstitution NALU H.264 (RFC 6184)
                byte nalHeader = span[0];
                byte nalType = (byte)(nalHeader & 0x1F);

                if (nalType >= 1 && nalType <= 23) // NALU unique
                {
                    _naluAssemblyStream.Write(new byte[] { 0x00, 0x00, 0x00, 0x01 });
                    _naluAssemblyStream.Write(span);
                }
                else if (nalType == 28) // FU-A Fragmenté
                {
                    if (span.Length < 2) return;
                    byte fuIndicator = span[0];
                    byte fuHeader = span[1];
                    bool start = (fuHeader & 0x80) != 0;
                    bool end = (fuHeader & 0x40) != 0;
                    byte reconstructedNal = (byte)((fuIndicator & 0xE0) | (fuHeader & 0x1F));

                    if (start)
                    {
                        _naluAssemblyStream.Write(new byte[] { 0x00, 0x00, 0x00, 0x01, reconstructedNal });
                    }
                    _naluAssemblyStream.Write(span.Slice(2));
                }

                if (packet.Marker) // Fin de la trame vidéo (Marker bit)
                {
                    var completeFrame = _naluAssemblyStream.ToArray();
                    _naluAssemblyStream.SetLength(0);

                    if (completeFrame.Length > 0)
                    {
                        OnVideoFrameAvailable?.Invoke(StreamName, completeFrame);
                    }
                }
            }
        }

        private void HandleAudioRtp(RtpPacket packet)
        {
            AudioPacketsReceivedCount++;
            var payload = packet.Payload.ToArray();

            OnAudioFrameAvailable?.Invoke(StreamName, payload);

            // Routage direct vers le mixeur broadcast AudioEngine si configuré
            if (_audioEngine != null)
            {
                var channelId = $"mobile_cam_{SlotNumber}";
                // Simulation / conversion échantillons PCM 16-bit 48kHz stéréo
                var sampleCount = packet.Payload.Length / 2;
                if (sampleCount > 0)
                {
                    var pcm = new short[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                    {
                        pcm[i] = (short)((packet.Payload.Span[i * 2] << 8) | packet.Payload.Span[(i * 2) + 1]);
                    }
                    _audioEngine.SubmitPcm(channelId, pcm);
                }
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                State = WebRtcReceiverState.Closed;
                _naluAssemblyStream.Dispose();
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
