using System.Diagnostics;
using System.Net.Http.Headers;

namespace EeccStudio.Broadcast;

/// <summary>
/// Encode le direct local en HLS et le dépose dans Supabase Storage,
/// pour que les téléphones hors du Wi-Fi de l'église puissent le lire.
/// </summary>
public sealed class PublicProgramRelay
{
    public const string PlaybackUrl =
        "https://rwojdiomftillhsamjal.supabase.co/storage/v1/object/public/live/program/index.m3u8";

    private static readonly PublicProgramRelay SharedRelay = new();
    private readonly object _gate = new();
    private Process? _ffmpeg;
    private CancellationTokenSource? _uploads;
    private CancellationTokenSource? _audioPump;
    private string? _directory;

    public static PublicProgramRelay Shared => SharedRelay;

    public string? Detail { get; private set; }

    /// <summary>PCM 16 bits stéréo 44,1 kHz produit par le mélangeur de la régie.</summary>
    public Func<int, byte[]>? RenderAudio { get; set; }

    public async Task<bool> StartAsync(string localPlaybackUrl, CancellationToken cancellationToken = default)
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg == null)
        {
            Detail = "ffmpeg est introuvable. L'installateur doit le placer dans le dossier ffmpeg et l'ajouter au PATH.";
            return false;
        }

        var port = ReadPort(localPlaybackUrl);
        lock (_gate)
        {
            if (_ffmpeg is { HasExited: false })
                return true;
            _directory = Path.Combine(Path.GetTempPath(), "eecc-live");
            Directory.CreateDirectory(_directory);
            foreach (var old in Directory.GetFiles(_directory))
            {
                try { File.Delete(old); } catch { /* un segment encore lu */ }
            }
        }

        var encoder = HardwareEncoderDetector.Detect(ffmpeg).H264EncoderName;
        if (!await LaunchAsync(ffmpeg, port, encoder, cancellationToken))
            await LaunchAsync(ffmpeg, port, "libx264", cancellationToken);

        if (_ffmpeg is not { HasExited: false })
        {
            Detail = "L'encodeur n'a pas démarré. Le direct reste sur le réseau local.";
            return false;
        }

        _uploads?.Cancel();
        _uploads = new CancellationTokenSource();
        _ = UploadLoop(_uploads.Token);
        var ready = await WaitForPlaylist(cancellationToken);
        Detail = ready
            ? "Flux public envoyé dans Supabase."
            : "Le flux public n'est pas encore prêt. Le direct reste sur le réseau local.";
        return ready;
    }

    public void Stop()
    {
        _uploads?.Cancel();
        _audioPump?.Cancel();
        lock (_gate)
        {
            try
            {
                if (_ffmpeg is { HasExited: false })
                {
                    _ffmpeg.Kill(entireProcessTree: true);
                    _ffmpeg.WaitForExit(1500);
                }
            }
            catch { /* déjà arrêté */ }
            _ffmpeg?.Dispose();
            _ffmpeg = null;
        }
    }

    private async Task<bool> LaunchAsync(string ffmpeg, int port, string encoder, CancellationToken cancellationToken)
    {
        Stop();
        var directory = _directory ?? Path.Combine(Path.GetTempPath(), "eecc-live");
        var video = encoder == "libx264"
            ? "-c:v libx264 -preset veryfast -tune zerolatency -pix_fmt yuv420p"
            : $"-c:v {encoder} -pix_fmt yuv420p";
        var args =
            "-hide_banner -loglevel error -fflags nobuffer " +
            $"-i http://127.0.0.1:{port}/live/stream " +
            "-f s16le -ar 44100 -ac 2 -probesize 32 -analyzeduration 0 -i pipe:0 " +
            "-map 0:v:0 -map 1:a:0 " +
            video + " -g 48 -c:a aac -b:a 128k -ar 44100 -ac 2 " +
            "-f hls -hls_time 2 -hls_list_size 6 -hls_flags delete_segments+append_list+omit_endlist " +
            $"-hls_segment_filename \"{Path.Combine(directory, "seg_%03d.ts")}\" \"{Path.Combine(directory, "index.m3u8")}\"";
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
            },
        };
        try
        {
            if (!process.Start())
                return false;
        }
        catch
        {
            return false;
        }

        var pump = new CancellationTokenSource();
        var stdin = process.StandardInput.BaseStream;
        _ = Task.Run(() => PumpAudio(stdin, pump.Token));
        lock (_gate)
        {
            _ffmpeg = process;
            _audioPump = pump;
        }
        await Task.Delay(1500, cancellationToken);
        return !process.HasExited;
    }

    private void PumpAudio(Stream stdin, CancellationToken cancellationToken)
    {
        const int frames = 4410;
        var silence = new byte[frames * 4];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pcm = RenderAudio?.Invoke(frames) ?? silence;
                if (pcm.Length != silence.Length)
                {
                    var fitted = new byte[silence.Length];
                    Buffer.BlockCopy(pcm, 0, fitted, 0, Math.Min(pcm.Length, fitted.Length));
                    pcm = fitted;
                }
                stdin.Write(pcm);
                stdin.Flush();
                Thread.Sleep(100);
            }
        }
        catch
        {
            // ffmpeg a fermé l'entrée.
        }
    }

    private async Task<bool> WaitForPlaylist(CancellationToken cancellationToken)
    {
        var playlist = Path.Combine(_directory ?? "", "index.m3u8");
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (File.Exists(playlist) && await UploadFile(playlist, "index.m3u8", cancellationToken))
                return true;
            await Task.Delay(700, cancellationToken);
        }
        return false;
    }

    private async Task UploadLoop(CancellationToken cancellationToken)
    {
        var seen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var directory = _directory;
                if (directory != null)
                {
                    foreach (var file in Directory.GetFiles(directory))
                    {
                        var stamp = File.GetLastWriteTimeUtc(file);
                        var name = Path.GetFileName(file);
                        if (seen.TryGetValue(name, out var previous) && previous == stamp)
                            continue;
                        if (await UploadFile(file, name, cancellationToken))
                            seen[name] = stamp;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Le prochain passage reprend l'envoi.
            }

            try { await Task.Delay(800, cancellationToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private static async Task<bool> UploadFile(string path, string name, CancellationToken cancellationToken)
    {
        var (url, key) = EeccCloud.Resolve();
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/storage/v1/object/live/program/{name}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add("apikey", key);
        request.Headers.Add("x-upsert", "true");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(name.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ? "application/vnd.apple.mpegurl" : "video/mp2t");
        using var response = await http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static int ReadPort(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Port > 0)
            return uri.Port;
        return 8089;
    }
}
