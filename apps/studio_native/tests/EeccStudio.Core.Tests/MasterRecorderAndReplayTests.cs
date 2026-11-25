using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EeccStudio.Broadcast;
using EeccStudio.Core.Network;
using Xunit;

namespace EeccStudio.Core.Tests
{
    public sealed class MasterRecorderAndReplayTests
    {
        [Fact]
        public async Task ReplayBuffer_AppendsFramesAndSavesReplay()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "Eecc_ReplayTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var replay = new ReplayBuffer(capacitySeconds: 10, expectedFps: 10, exportDir: tempDir);
                replay.Start();
                Assert.True(replay.IsActive);

                // Simulation de 15 trames (dépassant la capacité pour tester la fenêtre circulaire FIFO)
                byte[] sampleJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
                for (int i = 0; i < 15; i++)
                {
                    replay.AppendFrame(sampleJpeg);
                }

                Assert.True(replay.CurrentFramesCount > 0);

                var savedPath = await replay.SaveReplayAsync();
                Assert.NotNull(savedPath);
                Assert.True(File.Exists(savedPath));
                Assert.True(new FileInfo(savedPath).Length > 0);

                replay.Stop();
                Assert.False(replay.IsActive);
                Assert.Equal(0, replay.CurrentFramesCount);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void AdaptiveBitrateController_ReducesBitrateOnCongestion_AndRecovers()
        {
            var controller = new AdaptiveBitrateController(targetBitrateKbps: 4500, minBitrateKbps: 1500);
            Assert.Equal(4500, controller.CurrentBitrateKbps);
            Assert.False(controller.IsThrottled);

            // 1. Simuler congestion internet de l'église (RTT 600ms, perte 5%)
            controller.ReportNetworkStatus(rttMs: 600.0, packetLossPercent: 5.0, uploadThroughputKbps: 1200);
            controller.ReportNetworkStatus(rttMs: 650.0, packetLossPercent: 6.0, uploadThroughputKbps: 1000);

            // Le débit doit avoir été baissé automatiquement pour protéger le direct
            Assert.True(controller.CurrentBitrateKbps < 4500);
            Assert.True(controller.IsThrottled);

            // 2. Simuler rétablissement de la connexion
            int throttledBitrate = controller.CurrentBitrateKbps;
            controller.ReportNetworkStatus(rttMs: 50.0, packetLossPercent: 0.0, uploadThroughputKbps: 10000);
            controller.ReportNetworkStatus(rttMs: 40.0, packetLossPercent: 0.0, uploadThroughputKbps: 12000);

            Assert.True(controller.CurrentBitrateKbps >= throttledBitrate);
        }

        [Fact]
        public void VirtualCameraOutput_StartAndStop_OperatesSafely()
        {
            using var cam = new VirtualCameraOutput(1280, 720);
            Assert.False(cam.IsRunning);

            cam.Start();
            Assert.True(cam.IsRunning);

            // Envoi d'une trame BGRA de test
            byte[] dummyBgra = new byte[1280 * 720 * 4];
            cam.SendFrame(dummyBgra);

            cam.Stop();
            Assert.False(cam.IsRunning);
        }

        [Fact]
        public async Task ObsWebSocketServer_AnswersHelloAndIdentify()
        {
            // Port de test isolé
            int testPort = 14455;
            using var server = new ObsWebSocketServer(testPort);
            server.OnGetCurrentProgramScene = () => "Scene Culte Test";
            server.Start();
            Assert.True(server.IsRunning);

            using var ws = new ClientWebSocket();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                await ws.ConnectAsync(new Uri($"ws://localhost:{testPort}/"), cts.Token);
                Assert.Equal(WebSocketState.Open, ws.State);

                // 1. Réception de l'OpCode 0 : Hello
                var buffer = new byte[4096];
                var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                string helloJson = Encoding.UTF8.GetString(buffer, 0, res.Count);
                Assert.Contains("\"op\":0", helloJson);
                Assert.Contains("obsWebSocketVersion", helloJson);

                // 2. Envoi de l'OpCode 1 : Identify
                string identifyJson = "{\"op\":1,\"d\":{\"rpcVersion\":1}}";
                await ws.SendAsync(Encoding.UTF8.GetBytes(identifyJson), WebSocketMessageType.Text, true, cts.Token);

                // 3. Réception de l'OpCode 2 : Identified
                res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                string identifiedJson = Encoding.UTF8.GetString(buffer, 0, res.Count);
                Assert.Contains("\"op\":2", identifiedJson);
            }
            finally
            {
                server.Stop();
                if (ws.State == WebSocketState.Open)
                {
                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "End", CancellationToken.None); } catch { }
                }
            }
        }
    }
}
