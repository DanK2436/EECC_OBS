using System.Diagnostics;
using System.Text;
using EeccStudio.Core.Production;

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
        FfmpegProgramSink? closedSink = null;
        try
        {
            using var closedTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            closedSink = await FfmpegProgramSink.BeginRtmpAsync(ffmpeg!, Directory.CreateTempSubdirectory().FullName, "rtmp://127.0.0.1:1/live/closed", closedTimeout.Token);
            var blocked = new byte[FfmpegProgramSink.Width * FfmpegProgramSink.Height * 3];
            await Assert.ThrowsAnyAsync<Exception>(() => closedSink.PushAsync(blocked, new byte[FfmpegProgramSink.SamplesPerFrame * 2], closedTimeout.Token));
            Assert.Equal("failed", closedSink.Status);
            Assert.DoesNotContain("rtmp://127.0.0.1:1/live/closed", closedSink.SafeLog);
        }
        finally
        {
            if (closedSink != null) await closedSink.DisposeAsync();
        }

        var port = GetFreePort();
        var receivedDir = Directory.CreateTempSubdirectory().FullName;
        var received = Path.Combine(receivedDir, "received.flv");
        var url = "rtmp://127.0.0.1:" + port + "/live/synthetic";
        var stderr = new StringBuilder();
        var listener = new Process
        {
            StartInfo = new ProcessStartInfo(ffmpeg!)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
            },
        };
        foreach (var arg in new[] { "-y", "-hide_banner", "-loglevel", "info", "-listen", "1", "-rw_timeout", "8000000", "-i", url, "-c", "copy", received })
            listener.StartInfo.ArgumentList.Add(arg);
        listener.ErrorDataReceived += (_, args) => { if (args.Data != null) stderr.AppendLine(args.Data); };
        listener.Start();
        listener.BeginErrorReadLine();
        var ready = await WaitUntilListening(port, TimeSpan.FromSeconds(5));
        Assert.True(ready, "Récepteur RTMP non en écoute. stderr=" + stderr);
        var sourceDir = Directory.CreateTempSubdirectory().FullName;
        var source = FfmpegProgramSink.BeginRecording(ffmpeg!, sourceDir);
        var frame = new byte[FfmpegProgramSink.Width * FfmpegProgramSink.Height * 3];
        Array.Fill(frame, (byte)40);
        var tone = new byte[FfmpegProgramSink.SamplesPerFrame * 2];
        for (var i = 0; i < FfmpegProgramSink.FramesPerSecond; i++) source.Push(frame, tone);
        await source.DisposeAsync();
        var publish = await FfmpegProgramSink.PublishRawAsync(ffmpeg!, sourceDir, url);
        Assert.Equal(0, publish);
        if (!listener.WaitForExit(8000)) listener.Kill(entireProcessTree: true);
        var probe = await FfmpegProgramSink.ProbeAsync(ffprobe(ffmpeg!), received);
        Assert.Contains("video", probe.CodecTypes);
        Assert.Contains("audio", probe.CodecTypes);
        Assert.True(probe.DurationSeconds > 0.4, "durée reçue " + probe.DurationSeconds + " stderr=" + stderr);
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
