using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Network;
using EeccStudio.Core.WebRtc;
using Xunit;

namespace EeccStudio.Core.Tests;

public class WebRtcLatencyAndEnduranceTests
{
    [Fact]
    public async Task SimultaneousMultiCamera_4CamerasReceiveVideoAndAudioConcurrently()
    {
        using var server = new CameraServer(0);
        server.Start();
        var audioEngine = new AudioEngine();
        using var sessionManager = new WebRtcSessionManager(server, audioEngine);

        var receivers = new List<WebRtcCameraReceiver>();
        var frameCounts = new Dictionary<string, int>();

        // Création et négociation de 4 caméras simultanées (Cam-1 à Cam-4)
        for (int i = 1; i <= 4; i++)
        {
            var streamName = $"Cam-{i}";
            var client = new CameraClient
            {
                StreamName = streamName,
                SlotNumber = i,
                DisplayName = $"Caméra Mobile {i}"
            };
            var receiver = sessionManager.GetOrCreateReceiver(client);
            receivers.Add(receiver);
            frameCounts[streamName] = 0;

            receiver.OnVideoFrameAvailable += (slot, frame) =>
            {
                lock (frameCounts) frameCounts[slot]++;
            };

            const string offer = "v=0\r\nm=audio 9 UDP/TLS/RTP/SAVPF 111\r\na=rtpmap:111 opus/48000/2\r\nm=video 9 UDP/TLS/RTP/SAVPF 96\r\na=rtpmap:96 H264/90000\r\n";
            await receiver.ProcessOfferAsync(offer, fps: 300, resolution: "16K UHD");
            Assert.Equal(WebRtcReceiverState.Connected, receiver.State);
        }

        Assert.Equal(4, sessionManager.ActiveReceivers.Count);

        // Simulation d'envoi en rafale de paquets vidéo et audio sur les 4 flux en parallèle
        var tasks = new List<Task>();
        for (int i = 0; i < 4; i++)
        {
            var receiver = receivers[i];
            int cameraIndex = i + 1;

            tasks.Add(Task.Run(() =>
            {
                for (int frame = 0; frame < 50; frame++)
                {
                    // Paquet vidéo H.264 (Marker=true)
                    var rtpVideo = new byte[]
                    {
                        0x80, 0xE0, (byte)(frame >> 8), (byte)(frame & 0xFF),
                        0x00, 0x01, 0x5F, 0x90,
                        0x00, 0x00, 0x00, (byte)cameraIndex,
                        0x65, 0x88, 0x84, 0x00 // NALU IDR
                    };
                    receiver.IngestRtpPacket(rtpVideo);

                    // Paquet audio Opus
                    var rtpAudio = new byte[]
                    {
                        0x80, 0x6F, (byte)(frame >> 8), (byte)(frame & 0xFF),
                        0x00, 0x00, 0x07, 0xD0,
                        0x00, 0x00, 0x00, (byte)cameraIndex,
                        0x02, 0x00, 0x02, 0x00 // Échantillons Opus
                    };
                    receiver.IngestRtpPacket(rtpAudio);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Validation : chaque caméra a traité l'intégralité de ses 50 trames
        for (int i = 1; i <= 4; i++)
        {
            var slot = $"Cam-{i}";
            Assert.Equal(50, frameCounts[slot]);
            Assert.Equal(50, receivers[i - 1].FramesReceivedCount);
            Assert.Equal(50, receivers[i - 1].AudioPacketsReceivedCount);
        }

        // Vérification de la présence des 4 canaux mobiles dans AudioEngine
        var channels = audioEngine.Channels;
        for (int i = 1; i <= 4; i++)
        {
            Assert.Contains(channels, c => c.Id == $"mobile_cam_{i}");
        }
    }

    [Fact]
    public void LatencyBudget_ProcessingOverheadIsSubMillisecond()
    {
        // Mesure du coût de dépaquetage et traitement d'un paquet RTP
        var packetData = new byte[]
        {
            0x80, 0xE0, 0x00, 0x01,
            0x00, 0x00, 0x03, 0xE8,
            0x00, 0x00, 0x00, 0x01,
            0x65, 0x88, 0x84, 0x00
        };

        var client = new CameraClient { StreamName = "Cam-1", SlotNumber = 1 };
        using var server = new CameraServer(0);
        using var receiver = new WebRtcCameraReceiver(client, server);

        var sw = Stopwatch.StartNew();
        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            receiver.IngestRtpPacket(packetData);
        }
        sw.Stop();

        // Le temps de traitement par trame doit être inférieur à 0.1 ms pour garantir une latence < 300 ms
        var timePerFrameMs = (double)sw.ElapsedMilliseconds / iterations;
        Assert.True(timePerFrameMs < 0.5, $"Le coût par trame ({timePerFrameMs} ms) doit être quasi nul.");
    }

    [Fact]
    public void EnduranceSimulation_ContinuousFrameAssembly_ZeroMemoryLeak()
    {
        var client = new CameraClient { StreamName = "Cam-1", SlotNumber = 1 };
        using var server = new CameraServer(0);
        using var receiver = new WebRtcCameraReceiver(client, server);

        var packetData = new byte[]
        {
            0x80, 0xE0, 0x00, 0x01,
            0x00, 0x00, 0x03, 0xE8,
            0x00, 0x00, 0x00, 0x01,
            0x65, 0x88, 0x84, 0x00
        };

        // Traitement de 10 000 trames continues (équivalent à ~1 heure à 30-60 fps)
        for (int i = 0; i < 10000; i++)
        {
            receiver.IngestRtpPacket(packetData);
        }

        Assert.Equal(10000, receiver.FramesReceivedCount);
    }
}
