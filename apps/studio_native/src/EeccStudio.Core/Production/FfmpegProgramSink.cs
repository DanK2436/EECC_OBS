using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace EeccStudio.Core.Production;

public sealed class FfmpegProgramSink : IAsyncDisposable
{
    public const int Width = 320;
    public const int Height = 240;
    public const int FramesPerSecond = 25;
    public const int SampleRate = 8000;
    public const int SamplesPerFrame = SampleRate / FramesPerSecond;

    private readonly string _ffmpeg;
    private readonly string _directory;
    private readonly object _gate = new();
    private FileStream? _video;
    private FileStream? _audio;
    private Process? _live;
    private Stream? _liveVideo;
    private Stream? _liveAudio;
    private int _frames;
    private string? _outputPath;
    private readonly StringBuilder _log = new();

    private FfmpegProgramSink(string ffmpeg, string directory)
    {
        _ffmpeg = ffmpeg;
        _directory = directory;
    }

    public string Status { get; private set; } = "idle";
    public void Observe()
    {
        if (_live is { HasExited: true }) Status = "failed";
    }
    public string? OutputPath => _outputPath;
    public int FramesWritten => _frames;
    public string SafeLog => _log.ToString();

    public static string? Locate(string? baseDirectory = null)
    {
        var fromEnv = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv)) return fromEnv;
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            var beside = Path.Combine(baseDirectory, "ffmpeg", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            if (File.Exists(beside)) return beside;
        }
        var pathName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var probe = new ProcessStartInfo(pathName, "-version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        try
        {
            using var process = Process.Start(probe);
            process?.WaitForExit(2000);
            if (process is { ExitCode: 0 }) return pathName;
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
        return null;
    }

    public static FfmpegProgramSink BeginRecording(string ffmpeg, string directory)
    {
        Directory.CreateDirectory(directory);
        var sink = new FfmpegProgramSink(ffmpeg, directory)
        {
            _video = File.Create(Path.Combine(directory, "video.raw")),
            _audio = File.Create(Path.Combine(directory, "audio.raw")),
            Status = "recording",
        };
        return sink;
    }

    public static async Task<FfmpegProgramSink> BeginRtmpAsync(string ffmpeg, string directory, string url, CancellationToken cancellationToken)
    {
        var allowed = url.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("rtmps://", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(url) || !allowed)
        {
            throw new InvalidOperationException("URL RTMP absente ou non reconnue.");
        }
        Directory.CreateDirectory(directory);
        var audioName = "eecc-audio-" + Guid.NewGuid().ToString("N");
        var audioPath = OperatingSystem.IsWindows() ? @"\\.\pipe\" + audioName : Path.Combine(directory, audioName + ".fifo");
        if (!OperatingSystem.IsWindows())
        {
            using var mkfifo = Process.Start(new ProcessStartInfo("mkfifo", audioPath) { UseShellExecute = false, RedirectStandardError = true });
            mkfifo?.WaitForExit(2000);
        }
        var sink = new FfmpegProgramSink(ffmpeg, directory) { Status = "starting" };
        System.IO.Pipes.NamedPipeServerStream? server = null;
        if (OperatingSystem.IsWindows())
        {
            server = new System.IO.Pipes.NamedPipeServerStream(audioName, System.IO.Pipes.PipeDirection.Out, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
        }
        var process = new Process { StartInfo = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true } };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-xerror", "-rw_timeout", "3000000", "-f", "s16le", "-ar", SampleRate.ToString(CultureInfo.InvariantCulture), "-ac", "1", "-i", audioPath, "-f", "rawvideo", "-pix_fmt", "bgr24", "-video_size", Width + "x" + Height, "-framerate", FramesPerSecond.ToString(CultureInfo.InvariantCulture), "-i", "pipe:0", "-c:v", "flv", "-c:a", "aac", "-f", "flv", url })
            process.StartInfo.ArgumentList.Add(arg);
        process.ErrorDataReceived += (_, eventArgs) => sink.AppendLog(eventArgs.Data, url);
        if (!process.Start()) throw new InvalidOperationException("FFmpeg n'a pas démarré.");
        process.BeginErrorReadLine();
        sink._live = process;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            if (server != null)
            {
                await server.WaitForConnectionAsync(timeout.Token);
                sink._liveAudio = server;
            }
            else
            {
                sink._liveAudio = await OpenFifoAsync(audioPath, timeout.Token);
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            sink.Status = "failed";
            throw new TimeoutException("FFmpeg n'a pas ouvert l'audio dans le délai. " + sink.SafeLog);
        }
        sink._liveVideo = process.StandardInput.BaseStream;
        await Task.Delay(200, CancellationToken.None);
        sink.Status = process.HasExited ? "failed" : "publishing";
        if (process.HasExited) throw new InvalidOperationException("Encodeur arrêté avant la diffusion (code " + process.ExitCode + ").");
        return sink;
    }

    public async Task PushAsync(byte[] bgr, byte[] pcm, CancellationToken cancellationToken)
    {
        var write = Task.Run(() => Push(bgr, pcm), cancellationToken);
        var finished = await Task.WhenAny(write, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
        if (finished != write)
        {
            Status = "failed";
            try { if (_live is { HasExited: false }) _live.Kill(entireProcessTree: true); } catch { /* déjà arrêté */ }
            throw new TimeoutException("L'encodeur n'accepte plus les images.");
        }
        await write;
        Observe();
    }

    public void Push(byte[] bgr, ReadOnlySpan<byte> pcm)
    {
        if (bgr.Length != Width * Height * 3) throw new InvalidOperationException("Taille d'image inattendue.");
        var audio = new byte[SamplesPerFrame * 2];
        var copy = Math.Min(audio.Length, pcm.Length);
        pcm[..copy].CopyTo(audio);
        if (_liveVideo != null && _liveAudio != null)
        {
            var videoStream = _liveVideo;
            var audioStream = _liveAudio;
            var videoWrite = Task.Run(() => { videoStream.Write(bgr); videoStream.Flush(); });
            var audioWrite = Task.Run(() => { audioStream.Write(audio); audioStream.Flush(); });
            if (!Task.WaitAll(new[] { videoWrite, audioWrite }, 2000))
            {
                Status = "failed";
                try { if (_live is { HasExited: false }) _live.Kill(entireProcessTree: true); } catch { /* déjà arrêté */ }
                throw new TimeoutException("L'encodeur n'accepte plus les images.");
            }
            _frames++;
            return;
        }
        lock (_gate)
        {
            if (_video == null || _audio == null) return;
            _video.Write(bgr);
            _audio.Write(audio);
            _frames++;
        }
        if (_live is { HasExited: true }) Status = "failed";
    }

    public async Task<string?> FinalizeRecordingAsync()
    {
        FileStream? video;
        FileStream? audio;
        lock (_gate)
        {
            video = _video;
            audio = _audio;
            _video = null;
            _audio = null;
        }
        if (video == null || audio == null) return null;
        await video.DisposeAsync();
        await audio.DisposeAsync();
        if (_frames == 0)
        {
            Status = "failed";
            throw new InvalidOperationException("Aucune image du programme à enregistrer.");
        }
        var output = Path.Combine(_directory, "programme.mp4");
        var arguments = "-y -hide_banner -loglevel error -f rawvideo -pix_fmt bgr24 -video_size " + Width + "x" + Height +
                        " -framerate " + FramesPerSecond.ToString(CultureInfo.InvariantCulture) + " -i \"" + Path.Combine(_directory, "video.raw") +
                        "\" -f s16le -ar " + SampleRate + " -ac 1 -i \"" + Path.Combine(_directory, "audio.raw") +
                        "\" -c:v mpeg4 -q:v 5 -c:a aac -b:a 64k -movflags +faststart \"" + output + "\"";
        var exit = await RunAsync(_ffmpeg, arguments, null);
        if (exit != 0 || !File.Exists(output))
        {
            Status = "failed";
            throw new InvalidOperationException("FFmpeg n'a pas finalisé le fichier (code " + exit + ").");
        }
        _outputPath = output;
        Status = "stopped";
        return output;
    }

    public async Task StopAsync()
    {
        Stream? video;
        Stream? audio;
        Process? live;
        lock (_gate)
        {
            video = _liveVideo;
            audio = _liveAudio;
            live = _live;
            _liveVideo = null;
            _liveAudio = null;
            _live = null;
        }
        if (video != null) await video.DisposeAsync();
        if (audio != null) await audio.DisposeAsync();
        if (live != null && !live.HasExited)
        {
            if (!live.WaitForExit(4000)) live.Kill(entireProcessTree: true);
        }
        Status = live is { ExitCode: > 0 } ? "failed" : "stopped";
        live?.Dispose();
    }

    private static async Task<FileStream> OpenFifoAsync(string path, CancellationToken cancellationToken)
    {
        var open = Task.Run(() => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read), cancellationToken);
        var finished = await Task.WhenAny(open, Task.Delay(Timeout.Infinite, cancellationToken));
        if (finished != open) throw new OperationCanceledException(cancellationToken);
        return await open;
    }

    public static async Task<int> PublishRawAsync(string ffmpeg, string directory, string url)
    {
        var process = new Process { StartInfo = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true } };
        foreach (var arg in new[]
        {
            "-y", "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "bgr24", "-video_size", Width + "x" + Height,
            "-framerate", FramesPerSecond.ToString(CultureInfo.InvariantCulture), "-i", Path.Combine(directory, "video.raw"),
            "-f", "s16le", "-ar", SampleRate.ToString(CultureInfo.InvariantCulture), "-ac", "1", "-i", Path.Combine(directory, "audio.raw"),
            "-c:v", "flv", "-c:a", "aac", "-f", "flv", url,
        })
            process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Diffusion RTMP arrêtée (code " + process.ExitCode + "). " + error.Replace(url, "[cible]", StringComparison.Ordinal));
        return process.ExitCode;
    }

    public static async Task<int> RunAsync(string ffmpeg, string arguments, string? secret)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(ffmpeg, arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            },
        };
        process.Start();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (!string.IsNullOrWhiteSpace(secret)) error = error.Replace(secret, "[cible]", StringComparison.Ordinal);
        return process.ExitCode;
    }

    public static async Task<MediaProbe> ProbeAsync(string ffprobe, string mediaPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(ffprobe, "-v error -show_entries format=duration:stream=codec_type,codec_name -of json \"" + mediaPath + "\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.Start();
        var json = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var streams = document.RootElement.GetProperty("streams").EnumerateArray().Select(item => item.GetProperty("codec_type").GetString() ?? "").ToArray();
        var duration = document.RootElement.GetProperty("format").TryGetProperty("duration", out var value) ? double.Parse(value.GetString() ?? "0", CultureInfo.InvariantCulture) : 0;
        return new MediaProbe(streams, duration);
    }

    private void AppendLog(string? line, string secret)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var safe = line.Replace(secret, "[cible]", StringComparison.Ordinal);
        lock (_gate)
        {
            if (_log.Length < 4000) _log.AppendLine(safe);
        }
        if (_live is { HasExited: true }) Status = "failed";
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_video != null) await _video.DisposeAsync();
        if (_audio != null) await _audio.DisposeAsync();
    }
}

public sealed record MediaProbe(string[] CodecTypes, double DurationSeconds);
