using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Broadcast;
using EeccStudio.Core.Network;
using Xunit;

namespace EeccStudio.Broadcast.Tests;

public class LiveTransportTests
{
    private const string Key = "clef-test-locale";
    private const string Marker = "EECC-PROGRAMME-TEST";

    [Fact]
    public async Task Mode1_CameraServer_TvReceivesTheProgramFrame()
    {
        await using var session = new BroadcastSession();
        using var lease = StartServer();
        var server = lease.Server;
        var port = lease.Port;
        var frame = EeccTvFrames.WithComment(EeccTvFrames.Slate, Marker);
        server.ActiveProgramFrameBytes = frame;

        await AssertOffline(port);
        var snapshot = await session.StartAsync(
            TvOnly(),
            new CameraServerTvOutput(server),
            new FfmpegOutputFactory(),
            () => server.ActiveProgramFrameBytes,
            static frames => new byte[frames * 4]);

        Assert.True(snapshot.TvIsLive, snapshot.Summary);
        Assert.Equal("EECC TV seule — en direct.", snapshot.Summary);
        await AssertProgramFrame(port, frame);
        await AssertMjpegContains(port, Marker);

        var stopped = await session.StopAsync();
        Assert.False(server.IsStreaming);
        Assert.False(stopped.TvIsLive);
        await AssertOffline(port);
        Assert.DoesNotContain(Key, session.Log.Dump(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mode1_ConfigurableAdapter_ServesTheSameContract()
    {
        await using var ingest = new EeccTvHttpIngest("127.0.0.1", 0);
        await using var session = new BroadcastSession();
        var frame = EeccTvFrames.WithComment(EeccTvFrames.Slate, Marker);
        var snapshot = await session.StartAsync(
            new BroadcastPlan
            {
                Mode = BroadcastMode.TvOnly,
                Tv = new TvDestination { Port = 1 },
                Externals = Array.Empty<ExternalDestination>()
            },
            ingest,
            new FfmpegOutputFactory(),
            () => frame,
            static frames => new byte[frames * 4]);

        Assert.True(snapshot.TvIsLive, snapshot.Summary);
        Assert.True(ingest.Port > 0);
        await AssertProgramFrame(ingest.Port, frame);
        var status = await Get(ingest.Port, "/live/status");
        Assert.Contains("\"is_live\":true", status.Body, StringComparison.Ordinal);
        Assert.Contains("/live/stream", status.Body, StringComparison.Ordinal);

        await session.StopAsync();
        await AssertOffline(ingest.Port);
    }

    [SkippableFact(Timeout = 90000)]
    public async Task Mode2_TvAndOneLocalRtmpSink()
    {
        Skip.If(FfmpegLocator.Find() == null, "ffmpeg absent");
        await using var sink = await LocalRtmpSink.StartAsync(Key);
        var red = SolidJpeg("red");
        var blue = SolidJpeg("blue");
        await using var session = new BroadcastSession();
        using var lease = StartServer();
        var server = lease.Server;
        var port = lease.Port;
        var feed = new ProgramFeed();
        server.ActiveProgramFrameBytes = red;

        var snapshot = await session.StartAsync(
            Plan(BroadcastMode.TvPlusOne, sink),
            new CameraServerTvOutput(server),
            new FfmpegOutputFactory(),
            () => server.ActiveProgramFrameBytes,
            feed.Audio);

        Assert.True(snapshot.TvIsLive, snapshot.Summary);
        Assert.True(
            ExternalState(snapshot, "externe") == DestinationRunState.Live,
            string.Join(" | ", snapshot.Destinations));
        await AssertProgramFrame(port, red);
        server.ActiveProgramFrameBytes = blue;
        await Task.Delay(2000);
        await AssertProgramFrame(port, blue);
        AssertNoKey(snapshot, session);

        await session.StopAsync();
        await AssertSceneChangeAndTone(sink);
        await AssertOffline(port);
    }

    [SkippableFact(Timeout = 120000)]
    public async Task Mode3_TvAndThreeLocalSinks()
    {
        Skip.If(FfmpegLocator.Find() == null, "ffmpeg absent");
        await using var first = await LocalRtmpSink.StartAsync(Key);
        await using var second = await LocalRtmpSink.StartAsync(Key + "-b");
        await using var third = await LocalRtmpSink.StartAsync(Key + "-c");
        var red = SolidJpeg("red");
        var blue = SolidJpeg("blue");
        await using var session = new BroadcastSession();
        using var lease = StartServer();
        var server = lease.Server;
        var port = lease.Port;
        var feed = new ProgramFeed();
        server.ActiveProgramFrameBytes = red;

        var snapshot = await session.StartAsync(
            Plan(BroadcastMode.TvPlusThreeOrMore, first, second, third),
            new CameraServerTvOutput(server),
            new FfmpegOutputFactory(),
            () => server.ActiveProgramFrameBytes,
            feed.Audio);

        Assert.True(snapshot.TvIsLive, snapshot.Summary);
        Assert.True(
            snapshot.Destinations.Count(item => item.Kind == DestinationKind.External && item.State == DestinationRunState.Live) == 3,
            string.Join(" | ", snapshot.Destinations));
        await AssertProgramFrame(port, red);
        server.ActiveProgramFrameBytes = blue;
        await Task.Delay(2000);
        await AssertProgramFrame(port, blue);
        AssertNoKey(snapshot, session);

        await session.StopAsync();
        await AssertSceneChangeAndTone(first);
        await AssertSceneChangeAndTone(second);
        await AssertSceneChangeAndTone(third);
        await AssertOffline(port);
    }

    [SkippableFact(Timeout = 120000)]
    public async Task Mode3_ClosedPort_DoesNotCutTv()
    {
        Skip.If(FfmpegLocator.Find() == null, "ffmpeg absent");
        await using var first = await LocalRtmpSink.StartAsync(Key);
        await using var second = await LocalRtmpSink.StartAsync(Key + "-b");
        var closed = FreePort();
        var red = SolidJpeg("red");
        var blue = SolidJpeg("blue");
        var feed = new ProgramFeed();
        await using var session = new BroadcastSession();
        using var lease = StartServer();
        var server = lease.Server;
        var port = lease.Port;
        server.ActiveProgramFrameBytes = red;

        var snapshot = await session.StartAsync(
            new BroadcastPlan
            {
                Mode = BroadcastMode.TvPlusThreeOrMore,
                Tv = new TvDestination { Port = port },
                Externals = new[]
                {
                    Destination("a", first),
                    Destination("b", second),
                    new ExternalDestination
                    {
                        Id = "c",
                        DisplayName = "Fermée",
                        Platform = ExternalPlatform.Custom,
                        IngestUrl = $"rtmp://127.0.0.1:{closed}/live",
                        StreamKey = new StreamKey(Key)
                    }
                }
            },
            new CameraServerTvOutput(server),
            new FfmpegOutputFactory(),
            () => server.ActiveProgramFrameBytes,
            feed.Audio);

        Assert.True(snapshot.TvIsLive, snapshot.Summary);
        Assert.Equal(DestinationRunState.Error, snapshot.Destinations.Single(item => item.Id == "c").State);
        Assert.Equal(DestinationRunState.Live, ExternalState(snapshot, "a"));
        Assert.Equal(DestinationRunState.Live, ExternalState(snapshot, "b"));
        await AssertProgramFrame(port, red);
        server.ActiveProgramFrameBytes = blue;
        await Task.Delay(2000);
        await AssertProgramFrame(port, blue);
        AssertNoKey(snapshot, session);
        Assert.Contains("EECC TV", snapshot.Summary, StringComparison.Ordinal);

        await session.StopAsync();
        await AssertProgramStillAbsent(port);
        await AssertSceneChangeAndTone(first);
        await AssertSceneChangeAndTone(second);
    }

    private static async Task AssertSceneChangeAndTone(LocalRtmpSink sink)
    {
        var ready = await sink.HasVideoAsync();
        Assert.True(ready, "Flux local absent. " + sink.Describe());
        var duration = ProbeDuration(sink.MediaPath);
        Assert.True(duration > 1.2, "Le flux reçu est trop court pour contenir deux scènes : " + duration);
        var samples = new List<string>();
        (int R, int G, int B)? firstRed = null;
        (int R, int G, int B)? laterBlue = null;
        for (var time = 0.25; time < duration - 0.1; time += 0.35)
        {
            var color = AverageColor(sink.MediaPath, time);
            samples.Add($"{time:0.00}=({color.R},{color.G},{color.B})");
            if (firstRed == null && color.R > 140 && color.B < 90)
                firstRed = color;
            if (firstRed != null && color.B > 140 && color.R < 90)
                laterBlue = color;
        }

        Assert.True(firstRed != null && laterBlue != null, "Scènes successives absentes : " + string.Join(" ", samples));
        var tone = MeasureTone(sink.MediaPath);
        Assert.True(tone.Rms > 1500, "Le son reçu est silencieux ou trop faible : RMS " + tone.Rms);
        Assert.InRange(tone.Hertz, 800, 1300);
    }

    private static byte[] SolidJpeg(string color)
    {
        var path = Path.Combine(Path.GetTempPath(), "eecc-scene-" + color + ".jpg");
        if (!File.Exists(path) || new FileInfo(path).Length < 100)
        {
            var start = new ProcessStartInfo
            {
                FileName = FfmpegLocator.Find() ?? "ffmpeg",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (var arg in new[]
            {
                "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", $"color=c={color}:s=160x120",
                "-frames:v", "1", "-y", path
            })
                start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            process!.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }

        return File.ReadAllBytes(path);
    }

    private static double ProbeDuration(string media)
    {
        var start = new ProcessStartInfo
        {
            FileName = "ffprobe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", media })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        var text = process!.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static (int R, int G, int B) AverageColor(string media, double seconds)
    {
        var raw = Path.Combine(Path.GetTempPath(), "eecc-frame-" + Guid.NewGuid().ToString("N") + ".raw");
        var start = new ProcessStartInfo
        {
            FileName = FfmpegLocator.Find() ?? "ffmpeg",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var arg in new[]
        {
            "-hide_banner", "-loglevel", "error", "-ss", seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-i", media, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-y", raw
        })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        process!.WaitForExit();
        var bytes = File.Exists(raw) ? File.ReadAllBytes(raw) : Array.Empty<byte>();
        try { File.Delete(raw); } catch { }
        var pixels = bytes.Length / 3;
        Assert.True(pixels > 0, "Image décodée vide à " + seconds + " s.");
        long r = 0, g = 0, b = 0;
        for (var i = 0; i < pixels; i++)
        {
            r += bytes[i * 3];
            g += bytes[i * 3 + 1];
            b += bytes[i * 3 + 2];
        }

        return ((int)(r / pixels), (int)(g / pixels), (int)(b / pixels));
    }

    private static (double Rms, double Hertz) MeasureTone(string media)
    {
        var start = new ProcessStartInfo
        {
            FileName = FfmpegLocator.Find() ?? "ffmpeg",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-i", media, "-vn", "-ac", "1", "-ar", "44100", "-f", "s16le", "pipe:1" })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        using var buffer = new MemoryStream();
        process!.StandardOutput.BaseStream.CopyTo(buffer);
        process.WaitForExit();
        var raw = buffer.ToArray();
        var count = raw.Length / 2;
        Assert.True(count > 4000, "Pas assez d'échantillons audio.");
        double sum = 0;
        var previous = 0;
        var crossings = 0;
        for (var i = 0; i < count; i++)
        {
            var sample = BitConverter.ToInt16(raw, i * 2);
            sum += sample * (double)sample;
            if ((previous < 0 && sample >= 0) || (previous >= 0 && sample < 0))
                crossings++;
            previous = sample;
        }

        var seconds = count / 44100.0;
        return (Math.Sqrt(sum / count), crossings / 2.0 / seconds);
    }

    private sealed class ProgramFeed
    {
        private readonly AudioEngine _audio = new();
        private readonly object _gate = new();
        private double _phase;

        public ProgramFeed()
        {
            _audio.SetVolume("master", 1f);
        }

        public byte[] Audio(int frames)
        {
            lock (_gate)
            {
                var samples = new short[frames * 2];
                for (var i = 0; i < frames; i++)
                {
                    var sample = (short)(10000 * Math.Sin(_phase));
                    _phase += 2 * Math.PI * 1000.0 / ProgramAudioMix.SampleRate;
                    if (_phase > Math.PI * 2)
                        _phase -= Math.PI * 2;
                    samples[i * 2] = sample;
                    samples[i * 2 + 1] = sample;
                }

                _audio.SubmitPcm("programme", samples);
                return _audio.RenderMaster(frames);
            }
        }
    }

    private static void AssertNoKey(BroadcastSnapshot snapshot, BroadcastSession session)
    {
        Assert.DoesNotContain(Key, snapshot.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Key, session.Log.Dump(), StringComparison.Ordinal);
        foreach (var destination in snapshot.Destinations)
        {
            Assert.DoesNotContain(Key, destination.ToString(), StringComparison.Ordinal);
            if (destination.PlaybackUrl != null)
                Assert.DoesNotContain(Key, destination.PlaybackUrl, StringComparison.Ordinal);
        }
    }

    private static DestinationRunState ExternalState(BroadcastSnapshot snapshot, string id) =>
        snapshot.Destinations.Single(item => item.Id == id).State;

    private static BroadcastPlan TvOnly() => new()
    {
        Mode = BroadcastMode.TvOnly,
        Tv = new TvDestination(),
        Externals = Array.Empty<ExternalDestination>()
    };

    private static BroadcastPlan Plan(BroadcastMode mode, params LocalRtmpSink[] sinks)
    {
        var index = 0;
        var destinations = sinks.Select(sink =>
        {
            var id = index == 0 ? "externe" : ((char)('a' + index)).ToString();
            index++;
            return Destination(id, sink);
        }).ToArray();
        return new BroadcastPlan
        {
            Mode = mode,
            Tv = new TvDestination(),
            Externals = destinations
        };
    }

    private static ExternalDestination Destination(string id, LocalRtmpSink sink) => new()
    {
        Id = id,
        DisplayName = id,
        Platform = ExternalPlatform.Custom,
        IngestUrl = $"rtmp://127.0.0.1:{sink.Port}/live",
        StreamKey = new StreamKey(sink.Key)
    };

    private static RunningServer StartServer()
    {
        var port = FreePort();
        var server = new CameraServer(port);
        Assert.True(server.TryStart(out var detail), detail);
        return new RunningServer(server, port);
    }

    private sealed class RunningServer : IDisposable
    {
        public RunningServer(CameraServer server, int port)
        {
            Server = server;
            Port = port;
        }

        public CameraServer Server { get; }
        public int Port { get; }
        public void Dispose() => Server.Stop();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task AssertOffline(int port)
    {
        var status = await Get(port, "/live/status");
        Assert.Equal(200, status.Status);
        Assert.Contains("\"is_live\":false", status.Body, StringComparison.Ordinal);
        var frame = await Get(port, "/live/frame.jpg");
        Assert.Equal(404, frame.Status);
    }

    private static async Task AssertProgramStillAbsent(int port)
    {
        var frame = await Get(port, "/live/frame.jpg");
        Assert.Equal(404, frame.Status);
    }

    private static async Task AssertProgramFrame(int port, byte[] frame)
    {
        var status = await Get(port, "/live/status");
        Assert.Contains("\"is_live\":true", status.Body, StringComparison.Ordinal);
        Assert.Contains("/live/stream", status.Body, StringComparison.Ordinal);
        var response = await Get(port, "/live/frame.jpg");
        Assert.Equal(200, response.Status);
        Assert.Equal("image/jpeg", response.ContentType);
        Assert.Equal(frame, response.Bytes);
    }

    private static async Task AssertMjpegContains(int port, string marker)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        using var response = await client.GetAsync(
            $"http://127.0.0.1:{port}/live/stream",
            HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        Assert.Contains("multipart", type, StringComparison.OrdinalIgnoreCase);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[8192];
        using var collected = new MemoryStream();
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && collected.Length < 65536)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
                break;
            collected.Write(buffer, 0, read);
            if (Encoding.ASCII.GetString(collected.GetBuffer(), 0, (int)collected.Length).Contains(marker, StringComparison.Ordinal))
                return;
        }

        Assert.Fail("Le flux multipart lu par EECC TV ne contient pas l'image du programme.");
    }

    private static async Task<HttpBody> Get(int port, string path)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var response = await client.GetAsync($"http://127.0.0.1:{port}{path}");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return new HttpBody(
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            bytes,
            Encoding.UTF8.GetString(bytes));
    }

    private sealed record HttpBody(int Status, string ContentType, byte[] Bytes, string Body);

    private sealed class LocalRtmpSink : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _output;
        private readonly StringBuilder _stderr = new();

        private LocalRtmpSink(Process process, string output, int port, string key, StringBuilder stderr)
        {
            _process = process;
            _output = output;
            _stderr = stderr;
            Port = port;
            Key = key;
        }

        public int Port { get; }
        public string Key { get; }
        public string MediaPath => _output;

        public static async Task<LocalRtmpSink> StartAsync(string key)
        {
            var ffmpeg = FfmpegLocator.Find() ?? throw new InvalidOperationException("ffmpeg absent");
            var port = FreePort();
            var output = Path.Combine(Path.GetTempPath(), "eecc-rtmp-" + Guid.NewGuid().ToString("N") + ".flv");
            var start = new ProcessStartInfo
            {
                FileName = ffmpeg,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (var arg in new[]
            {
                "-hide_banner", "-loglevel", "error",
                "-listen", "1",
                "-i", $"rtmp://127.0.0.1:{port}/live/{key}",
                "-c", "copy", "-flvflags", "no_duration_filesize", "-y", output
            })
                start.ArgumentList.Add(arg);

            var process = Process.Start(start) ?? throw new InvalidOperationException("ffmpeg n'a pas démarré");
            var stderr = new StringBuilder();
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                    stderr.AppendLine(eventArgs.Data);
            };
            process.OutputDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            var deadline = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                    throw new InvalidOperationException("Le récepteur RTMP local s'est arrêté immédiatement.");
                if (IsListening(port))
                {
                    await Task.Delay(100);
                    return new LocalRtmpSink(process, output, port, key, stderr);
                }
                await Task.Delay(40);
            }

            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("Le récepteur RTMP local n'écoute pas.");
        }

        public string Describe()
        {
            var length = File.Exists(_output) ? new FileInfo(_output).Length : -1;
            var stderr = _stderr.ToString().Replace(Key, "***", StringComparison.Ordinal);
            return $"octets={length} termine={_process.HasExited} code={(_process.HasExited ? _process.ExitCode : -1)} stderr={stderr}";
        }

        public async Task<bool> HasVideoAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while (!_process.HasExited && DateTime.UtcNow < deadline)
                await Task.Delay(100);
            if (!_process.HasExited)
            {
                try { _process.Kill(entireProcessTree: true); } catch { }
                await _process.WaitForExitAsync();
            }

            var length = File.Exists(_output) ? new FileInfo(_output).Length : 0;
            if (length < 32)
                return false;

            var probe = new ProcessStartInfo
            {
                FileName = "ffprobe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            probe.ArgumentList.Add("-v");
            probe.ArgumentList.Add("error");
            probe.ArgumentList.Add("-show_entries");
            probe.ArgumentList.Add("stream=codec_type");
            probe.ArgumentList.Add("-of");
            probe.ArgumentList.Add("csv=p=0");
            probe.ArgumentList.Add(_output);
            using var process = Process.Start(probe);
            if (process == null)
                return false;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output.Contains("video", StringComparison.OrdinalIgnoreCase);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
            catch
            {
            }
            finally
            {
                _process.Dispose();
                try { if (File.Exists(_output)) File.Delete(_output); } catch { }
            }
        }

        private static bool IsListening(int port)
        {
            var needle = ":" + port.ToString("X4");
            foreach (var table in new[] { "/proc/net/tcp", "/proc/net/tcp6" })
            {
                if (!File.Exists(table))
                    continue;
                foreach (var line in File.ReadLines(table))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4 || parts[3] != "0A")
                        continue;
                    if (parts[1].EndsWith(needle, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
