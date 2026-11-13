using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using EeccStudio.Core.Production;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EeccStudio.Core.Tests;

public class ProductionTests
{
    public const string SourceLabel = "SYNTHETIC_TEST_SOURCE";

    [Fact]
    public void Scenes_persist_and_cut_changes_only_the_program()
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "scenes.json");
        var document = SceneStore.CreateDefault();
        SceneStore.Save(path, document);
        var loaded = SceneStore.LoadOrCreate(path);
        var director = new ProgramDirector(loaded);
        Assert.NotEqual(director.Document.PreviewSceneId, director.Document.ProgramSceneId);
        director.SelectPreview(director.Document.ProgramSceneId!);
        var preview = director.Preview;
        director.Cut();
        Assert.Equal(preview!.Id, director.Program!.Id);
        var again = SceneStore.LoadOrCreate(path);
        again.PreviewSceneId = again.ProgramSceneId;
        SceneStore.Save(path, again);
        Assert.Equal(again.PreviewSceneId, SceneStore.LoadOrCreate(path).PreviewSceneId);
    }

    [Fact]
    public void Dissolve_blends_real_program_pixels()
    {
        var document = SceneStore.CreateDefault();
        var color = document.Scenes.Single(scene => scene.Source == SceneSourceKind.Color);
        color.ColorHex = "#0000FF";
        document.ProgramSceneId = color.Id;
        var other = document.Scenes.Single(scene => scene.Source == SceneSourceKind.Camera);
        other.Source = SceneSourceKind.Color;
        other.ColorHex = "#00FF00";
        document.PreviewSceneId = other.Id;
        var director = new ProgramDirector(document);
        var before = director.RenderProgram(null, 4, 2);
        director.BeginDissolve(before, 2);
        var mid = director.RenderProgram(null, 4, 2);
        var end = director.RenderProgram(null, 4, 2);
        Assert.NotEqual(before[0], mid[0]);
        Assert.Equal(0x00, end[0]);
        Assert.Equal(0xFF, end[1]);
        Assert.Equal(0x00, end[2]);
    }

    [Fact]
    public async Task Synthetic_program_file_has_picture_sound_and_duration()
    {
        Assert.Equal("SYNTHETIC_TEST_SOURCE", SourceLabel);
        var ffmpeg = FfmpegProgramSink.Locate();
        Assert.True(ffmpeg != null, "FFmpeg est introuvable pour le test média.");
        var directory = Directory.CreateTempSubdirectory().FullName;
        var sink = FfmpegProgramSink.BeginRecording(ffmpeg!, directory);
        var frame = new byte[FfmpegProgramSink.Width * FfmpegProgramSink.Height * 3];
        var tone = new byte[FfmpegProgramSink.SamplesPerFrame * 2];
        for (var i = 0; i < FfmpegProgramSink.FramesPerSecond; i++)
        {
            frame[i % frame.Length] = 220;
            for (var sample = 0; sample < FfmpegProgramSink.SamplesPerFrame; sample++)
            {
                var value = (short)(Math.Sin((i * FfmpegProgramSink.SamplesPerFrame + sample) / 8d) * 8000);
                tone[sample * 2] = (byte)value;
                tone[sample * 2 + 1] = (byte)(value >> 8);
            }
            sink.Push(frame, tone);
        }
        var output = await sink.FinalizeRecordingAsync();
        var probe = await FfmpegProgramSink.ProbeAsync(ffprobe(ffmpeg!), output!);
        Assert.Contains("video", probe.CodecTypes);
        Assert.Contains("audio", probe.CodecTypes);
        Assert.InRange(probe.DurationSeconds, 0.8, 1.4);
    }

    [Fact]
    public async Task Synthetic_rtmp_is_received_locally_and_a_closed_port_fails()
    {
        var ffmpeg = FfmpegProgramSink.Locate();
        Assert.True(ffmpeg != null, "FFmpeg est introuvable pour le test RTMP.");
        var encoder = await FfmpegProgramSink.ResolveH264EncoderAsync(ffmpeg!);
        Assert.True(encoder is "libx264" or "h264_mf", "Encodeur H.264 redistribuable introuvable.");
        FfmpegProgramSink? closedSink = null;
        try
        {
            using var closedTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try
            {
                closedSink = await FfmpegProgramSink.BeginRtmpAsync(ffmpeg!, Directory.CreateTempSubdirectory().FullName, "rtmp://127.0.0.1:1/live/closed", closedTimeout.Token);
            }
            catch (Exception ex)
            {
                Assert.DoesNotContain("rtmp://127.0.0.1:1/live/closed", ex.ToString());
                closedSink = null;
            }
            if (closedSink != null)
            {
                var until = DateTime.UtcNow.AddSeconds(6);
                var blocked = new byte[FfmpegProgramSink.Width * FfmpegProgramSink.Height * 3];
                var silence = new byte[FfmpegProgramSink.SamplesPerFrame * 2];
                while (DateTime.UtcNow < until && closedSink.Status != "failed")
                {
                    try { await closedSink.PushAsync(blocked, silence, closedTimeout.Token); }
                    catch { closedSink.Observe(); break; }
                    closedSink.Observe();
                    await Task.Delay(100);
                }
                Assert.Equal("failed", closedSink.Status);
                Assert.DoesNotContain("rtmp://127.0.0.1:1/live/closed", closedSink.SafeLog);
            }
        }
        finally
        {
            if (closedSink != null) await closedSink.DisposeAsync();
        }

        var listenPort = GetFreePort();
        var received = Path.Combine(Directory.CreateTempSubdirectory().FullName, "received.flv");
        var stderr = new StringBuilder();
        var listener = new Process { StartInfo = new ProcessStartInfo(ffmpeg!) { UseShellExecute = false, RedirectStandardError = true } };
        foreach (var arg in new[] { "-y", "-hide_banner", "-loglevel", "error", "-listen", "1", "-rw_timeout", "8000000", "-i", "rtmp://127.0.0.1:" + listenPort + "/live/synthetic", "-c", "copy", received })
            listener.StartInfo.ArgumentList.Add(arg);
        listener.ErrorDataReceived += (_, args) => { if (args.Data != null) stderr.AppendLine(args.Data); };
        listener.Start();
        listener.BeginErrorReadLine();
        using var bridge = new CountingBridge(listenPort);
        var ready = await WaitUntilListening(listenPort, TimeSpan.FromSeconds(5));
        Assert.True(ready, "Récepteur RTMP non en écoute. stderr=" + stderr);
        var url = "rtmp://127.0.0.1:" + bridge.Port + "/live/synthetic";
        FfmpegProgramSink? publisher = null;
        try
        {
            using var publishTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            publisher = await FfmpegProgramSink.BeginRtmpAsync(ffmpeg!, Directory.CreateTempSubdirectory().FullName, url, publishTimeout.Token);
            var frame = new byte[FfmpegProgramSink.Width * FfmpegProgramSink.Height * 3];
            var tone = new byte[FfmpegProgramSink.SamplesPerFrame * 2];
            for (var i = 0; i < 12; i++)
            {
                frame[0] = (byte)(20 + i * 15);
                frame[1] = (byte)(i * 10);
                await publisher.PushAsync(frame, tone, publishTimeout.Token);
            }
            var receivedBeforeStop = await WaitForBytes(bridge, 4500, TimeSpan.FromSeconds(3));
            Assert.True(publisher.EncoderRunning, "L'encodeur s'est arrêté avant la fin de l'émission. " + publisher.SafeLog);
            Assert.True(receivedBeforeStop, "Aucun octet média reçu avant l'arrêt. octets=" + bridge.FromPublisher + " stderr=" + stderr);
            for (var i = 0; i < 12; i++)
            {
                frame[0] = 255;
                frame[2] = (byte)(i * 12);
                await publisher.PushAsync(frame, tone, publishTimeout.Token);
            }
            await publisher.StopAsync();
            var fileDeadline = DateTime.UtcNow.AddSeconds(6);
            while (DateTime.UtcNow < fileDeadline && !File.Exists(received))
                await Task.Delay(100);
        }
        finally
        {
            if (publisher != null) await publisher.DisposeAsync();
            if (!listener.HasExited && !listener.WaitForExit(3000)) listener.Kill(entireProcessTree: true);
        }
        Assert.True(File.Exists(received) && new FileInfo(received).Length > 500, "fichier reçu " + (File.Exists(received) ? new FileInfo(received).Length.ToString() : "absent") + " stderr=" + stderr);
        var probe = await FfmpegProgramSink.ProbeAsync(ffprobe(ffmpeg!), received);
        Assert.Contains("h264", probe.CodecNames);
        Assert.Contains("aac", probe.CodecNames);
        Assert.True(probe.DurationSeconds > 0.4, "durée reçue " + probe.DurationSeconds + " stderr=" + stderr);
        Assert.DoesNotContain(url, publisher!.SafeLog);
        var first = await ExtractFrameAsync(ffmpeg!, received, 0);
        var later = await ExtractFrameAsync(ffmpeg!, received, 8);
        Assert.Equal(FfmpegProgramSink.Width * FfmpegProgramSink.Height * 3, first.Length);
        Assert.Equal(first.Length, later.Length);
        Assert.NotEqual(first[0], later[0]);
    }

    private static async Task<bool> WaitForBytes(CountingBridge bridge, int minimum, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Volatile.Read(ref bridge.FromPublisher) >= minimum) return true;
            await Task.Delay(50);
        }
        return Volatile.Read(ref bridge.FromPublisher) >= minimum;
    }

    private static async Task<byte[]> ExtractFrameAsync(string ffmpeg, string media, int index)
    {
        var output = Path.Combine(Directory.CreateTempSubdirectory().FullName, "frame.bgr");
        var process = new Process { StartInfo = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, RedirectStandardError = true } };
        foreach (var arg in new[] { "-y", "-i", media, "-vf", "select=eq(n\\," + index + ")", "-fps_mode", "passthrough", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "bgr24", output })
            process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        await process.WaitForExitAsync();
        return File.Exists(output) ? await File.ReadAllBytesAsync(output) : Array.Empty<byte>();
    }

    private sealed class CountingBridge : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancel = new();
        public int FromPublisher;
        public int Port { get; }

        public CountingBridge(int targetPort)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = AcceptAsync(targetPort);
        }

        private async Task AcceptAsync(int targetPort)
        {
            try
            {
                var incoming = await _listener.AcceptTcpClientAsync(_cancel.Token);
                using var outgoing = new TcpClient();
                await outgoing.ConnectAsync(IPAddress.Loopback, targetPort);
                var forward = PumpAsync(incoming.GetStream(), outgoing.GetStream(), count: true);
                var backward = PumpAsync(outgoing.GetStream(), incoming.GetStream(), count: false);
                await Task.WhenAll(forward, backward);
                incoming.Dispose();
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        private async Task PumpAsync(NetworkStream from, NetworkStream to, bool count)
        {
            var buffer = new byte[8192];
            try
            {
                while (!_cancel.IsCancellationRequested)
                {
                    var read = await from.ReadAsync(buffer, _cancel.Token);
                    if (read == 0)
                    {
                        try { to.Socket.Shutdown(SocketShutdown.Send); } catch { /* déjà fermée */ }
                        break;
                    }
                    if (count) Interlocked.Add(ref FromPublisher, read);
                    await to.WriteAsync(buffer.AsMemory(0, read), _cancel.Token);
                }
            }
            catch (Exception) { /* fin de connexion */ }
        }

        public void Dispose()
        {
            _cancel.Cancel();
            _listener.Stop();
        }
    }

    private static async Task<bool> WaitUntilListening(int port, TimeSpan timeout)
    {
        var hex = port.ToString("X4");
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (OperatingSystem.IsWindows())
            {
                var probe = Process.Start(new ProcessStartInfo("netstat", "-an") { UseShellExecute = false, RedirectStandardOutput = true });
                var text = probe?.StandardOutput.ReadToEnd() ?? "";
                probe?.WaitForExit(2000);
                if (text.Contains(":" + port, StringComparison.Ordinal) && text.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            if (OperatingSystem.IsLinux() && File.Exists("/proc/net/tcp"))
            {
                foreach (var line in File.ReadAllLines("/proc/net/tcp").Skip(1))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 3 && parts[1].EndsWith(":" + hex, StringComparison.OrdinalIgnoreCase) && parts[3] == "0A")
                        return true;
                }
            }
            await Task.Delay(100);
        }
        return false;
    }

    private static string ffprobe(string ffmpeg)
    {
        var candidate = Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
        return File.Exists(candidate) ? candidate : "ffprobe";
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
