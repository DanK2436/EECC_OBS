using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace EeccStudio.Broadcast;

public sealed class FfmpegRtmpOutput : IExternalOutput
{
    private const int FramesPerTick = 4410;
    private readonly ExternalDestination _destination;
    private readonly string[] _secrets;
    private readonly StringBuilder _stderr = new();
    private readonly List<ProgramTube> _tubes = new();
    private Process? _process;
    private volatile bool _stopping;

    public FfmpegRtmpOutput(ExternalDestination destination)
    {
        _destination = destination;
        _secrets = new[] { destination.StreamKey.Reveal() };
    }

    public string DestinationId => _destination.Id;

    public event Action<DestinationRunState, string?>? StateChanged;

    public async Task<OutputStart> StartAsync(
        Func<byte[]?> currentFrame,
        Func<int, byte[]> renderAudio,
        CancellationToken cancellationToken)
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg == null)
        {
            const string missing = "Encodeur ffmpeg introuvable (libx264 et AAC requis, variable EECC_FFMPEG). EECC TV n'est pas coupée.";
            return new OutputStart(DestinationRunState.Error, missing);
        }

        string url;
        try
        {
            url = PublishUrl.Build(_destination);
        }
        catch (Exception ex)
        {
            return new OutputStart(DestinationRunState.Error, SecretRedactor.Redact(ex.Message, _secrets));
        }

        ProgramTube video;
        ProgramTube audio;
        try
        {
            video = ProgramTube.Create("video");
            audio = ProgramTube.Create("audio");
            _tubes.Add(video);
            _tubes.Add(audio);
        }
        catch (Exception ex)
        {
            CleanupTubes();
            return new OutputStart(DestinationRunState.Error, SecretRedactor.Redact(ex.Message, _secrets));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        AddArguments(startInfo, video.FfmpegPath, audio.FfmpegPath);
        startInfo.ArgumentList.Add(url);

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("ffmpeg n'a pas démarré.");
        }
        catch (Exception ex)
        {
            CleanupTubes();
            return new OutputStart(DestinationRunState.Error, SecretRedactor.Redact(ex.Message, _secrets.Append(url)));
        }

        _process = process;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, eventArgs) => AppendStderr(eventArgs.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var videoPump = Task.Run(() => PumpVideo(process, video, currentFrame));
        var audioPump = Task.Run(() => PumpAudio(process, audio, renderAudio));

        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline && !HasStartedOutput())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited || HasFatalStderr())
            {
                await StopAsync();
                return new OutputStart(DestinationRunState.Error, FailureMessage());
            }

            await Task.Delay(100, cancellationToken);
        }

        if (process.HasExited || HasFatalStderr() || !HasStartedOutput())
        {
            await StopAsync();
            return new OutputStart(DestinationRunState.Error, FailureMessage());
        }

        _ = Task.Run(async () =>
        {
            try { await process.WaitForExitAsync(); }
            catch { return; }
            if (_stopping)
                return;
            StateChanged?.Invoke(DestinationRunState.Error, "L'envoi vers cette destination s'est arrêté. EECC TV n'est pas coupée.");
        });
        _ = videoPump;
        _ = audioPump;
        return new OutputStart(DestinationRunState.Live, null);
    }

    public async Task StopAsync()
    {
        _stopping = true;
        var process = _process;
        CleanupTubes();
        if (process != null)
        {
            try
            {
                if (!process.HasExited)
                {
                    var finished = await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(TimeSpan.FromSeconds(3)));
                    if (!process.HasExited)
                    {
                        try { process.Kill(entireProcessTree: true); } catch { }
                        await process.WaitForExitAsync();
                    }

                    _ = finished;
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
                _process = null;
            }
        }
    }

    private void PumpVideo(Process process, ProgramTube video, Func<byte[]?> currentFrame)
    {
        try
        {
            video.WaitConnected(process, TimeSpan.FromSeconds(8));
            while (!_stopping && !process.HasExited)
            {
                var frame = currentFrame() ?? EeccTvFrames.Slate;
                if (frame.Length < 4 || frame[0] != 0xFF || frame[1] != 0xD8)
                    frame = EeccTvFrames.Slate;
                video.Write(frame);
                Thread.Sleep(90);
            }
        }
        catch (Exception ex)
        {
            if (!_stopping)
                AppendStderr("flux-video: " + ex.GetType().Name);
        }
    }

    private void PumpAudio(Process process, ProgramTube audio, Func<int, byte[]> renderAudio)
    {
        try
        {
            audio.WaitConnected(process, TimeSpan.FromSeconds(8));
            var expected = FramesPerTick * 4;
            while (!_stopping && !process.HasExited)
            {
                var pcm = renderAudio(FramesPerTick) ?? Array.Empty<byte>();
                if (pcm.Length != expected)
                {
                    var fitted = new byte[expected];
                    Buffer.BlockCopy(pcm, 0, fitted, 0, Math.Min(pcm.Length, expected));
                    pcm = fitted;
                }

                audio.Write(pcm);
                Thread.Sleep(90);
            }
        }
        catch (Exception ex)
        {
            if (!_stopping)
                AppendStderr("flux-audio: " + ex.GetType().Name);
        }
    }

    private static void AddArguments(ProcessStartInfo startInfo, string video, string audio)
    {
        string[] args =
        {
            "-hide_banner", "-loglevel", "info",
            "-f", "s16le", "-ar", "44100", "-ac", "2", "-probesize", "32", "-analyzeduration", "0", "-i", audio,
            "-f", "image2pipe", "-vcodec", "mjpeg", "-framerate", "10", "-i", video,
            "-map", "1:v:0", "-map", "0:a:0",
            "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency", "-pix_fmt", "yuv420p",
            "-g", "10", "-b:v", "400k",
            "-c:a", "aac", "-b:a", "128k", "-ar", "44100", "-ac", "2",
            "-flush_packets", "1",
            "-f", "flv"
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);
    }

    private void CleanupTubes()
    {
        foreach (var tube in _tubes)
            tube.Dispose();
        _tubes.Clear();
    }

    private void AppendStderr(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        var redacted = SecretRedactor.Redact(line, _secrets);
        lock (_stderr)
        {
            if (_stderr.Length > 4000)
                return;
            _stderr.AppendLine(redacted);
        }
    }

    private bool HasStartedOutput()
    {
        string text;
        lock (_stderr)
            text = _stderr.ToString();
        return text.Contains("Output #0", StringComparison.Ordinal)
            || text.Contains("frame=", StringComparison.Ordinal);
    }

    private bool HasFatalStderr()
    {
        string text;
        lock (_stderr)
            text = _stderr.ToString();
        return text.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Error opening output", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Cannot open connection", StringComparison.OrdinalIgnoreCase)
            || text.Contains("I/O error", StringComparison.OrdinalIgnoreCase)
            || text.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Server error", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Immediate exit", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Invalid data", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Could not find codec parameters", StringComparison.OrdinalIgnoreCase)
            || text.Contains("flux-video:", StringComparison.Ordinal)
            || text.Contains("flux-audio:", StringComparison.Ordinal);
    }

    private string FailureMessage()
    {
        string text;
        lock (_stderr)
            text = _stderr.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
            return "La destination a refusé la connexion. EECC TV n'est pas coupée.";
        return "La destination a refusé la connexion. EECC TV n'est pas coupée. " + text;
    }
}

internal sealed class ProgramTube : IDisposable
{
    private readonly string? _fifoPath;
    private NamedPipeServerStream? _server;
    private Stream? _stream;

    private ProgramTube(string ffmpegPath, string? fifoPath, NamedPipeServerStream? server)
    {
        FfmpegPath = ffmpegPath;
        _fifoPath = fifoPath;
        _server = server;
    }

    public string FfmpegPath { get; }

    public static ProgramTube Create(string role)
    {
        var id = Guid.NewGuid().ToString("N");
        if (OperatingSystem.IsWindows())
        {
            var name = "eecc-" + role + "-" + id;
            var server = new NamedPipeServerStream(name, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            return new ProgramTube(@"\\.\pipe\" + name, null, server);
        }

        var path = Path.Combine(Path.GetTempPath(), "eecc-" + role + "-" + id);
        if (mkfifo(path, 438) != 0)
            throw new IOException("Impossible de créer le tube du programme.");
        return new ProgramTube(path, path, null);
    }

    public void WaitConnected(Process process, TimeSpan timeout)
    {
        if (_server != null)
        {
            var wait = _server.WaitForConnectionAsync();
            if (!wait.Wait(timeout))
                throw new TimeoutException("Le tube du programme n'a pas été ouvert.");
            _stream = _server;
            return;
        }

        var path = _fifoPath ?? throw new InvalidOperationException("Tube absent.");
        var open = Task.Run(() => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read));
        var until = DateTime.UtcNow + timeout;
        while (!open.IsCompleted && DateTime.UtcNow < until)
        {
            if (process.HasExited)
            {
                try { File.Delete(path); } catch { }
            }

            Thread.Sleep(20);
        }

        if (!open.IsCompleted)
            throw new TimeoutException("Le tube du programme n'a pas été ouvert.");
        _stream = open.GetAwaiter().GetResult();
    }

    public void Write(byte[] data)
    {
        var stream = _stream ?? throw new InvalidOperationException("Tube non ouvert.");
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }

    public void Dispose()
    {
        try { _stream?.Dispose(); } catch { }
        try { _server?.Dispose(); } catch { }
        if (_fifoPath != null)
        {
            try { if (File.Exists(_fifoPath)) File.Delete(_fifoPath); } catch { }
        }

        _stream = null;
        _server = null;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
    private static extern int mkfifo(string path, int mode);
}
