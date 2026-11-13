using System.Diagnostics;
using System.Text;

namespace EeccStudio.Broadcast;

public sealed record TvOutputResult(bool IsLive, string? PlaybackUrl, string? Error);

public sealed record OutputStart(DestinationRunState State, string? Error);

public interface ITvOutput
{
    Task<TvOutputResult> StartAsync(byte[]? programFrame, CancellationToken cancellationToken);

    Task StopAsync();
}

public interface IExternalOutput
{
    string DestinationId { get; }

    Task<OutputStart> StartAsync(Func<byte[]?> currentFrame, CancellationToken cancellationToken);

    Task StopAsync();

    event Action<DestinationRunState, string?>? StateChanged;
}

public interface IExternalOutputFactory
{
    IExternalOutput Create(ExternalDestination destination);
}

public sealed class DestinationStatus
{
    private DestinationStatus(
        string id,
        string displayName,
        DestinationKind kind,
        ExternalPlatform? platform,
        DestinationRunState state,
        string? error,
        string? playbackUrl)
    {
        Id = id;
        DisplayName = displayName;
        Kind = kind;
        Platform = platform;
        State = state;
        Error = error;
        PlaybackUrl = playbackUrl;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public DestinationKind Kind { get; }

    public ExternalPlatform? Platform { get; }

    public DestinationRunState State { get; }

    public string? Error { get; }

    public string? PlaybackUrl { get; }

    public static DestinationStatus Tv(DestinationRunState state, string? playbackUrl, string? error) =>
        new("eecc-tv", "EECC TV", DestinationKind.EeccTv, null, state, error, playbackUrl);

    public static DestinationStatus External(
        ExternalDestination destination,
        DestinationRunState state,
        string? error) =>
        new(
            destination.Id,
            destination.DisplayName,
            DestinationKind.External,
            destination.Platform,
            state,
            error,
            PublishUrl.Redacted(destination.IngestUrl));

    public DestinationStatus With(DestinationRunState state, string? error) =>
        new(Id, DisplayName, Kind, Platform, state, error, PlaybackUrl);

    public override string ToString() =>
        $"{DisplayName} — {State}" + (string.IsNullOrWhiteSpace(Error) ? string.Empty : " — " + Error);
}

public sealed class BroadcastSnapshot
{
    private BroadcastSnapshot(
        bool accepted,
        IReadOnlyList<DestinationStatus> destinations,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings)
    {
        Accepted = accepted;
        Destinations = destinations;
        Errors = errors;
        Warnings = warnings;
    }

    public bool Accepted { get; }

    public IReadOnlyList<DestinationStatus> Destinations { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<string> Warnings { get; }

    public bool TvIsLive =>
        Destinations.Any(destination => destination.Kind == DestinationKind.EeccTv && destination.State == DestinationRunState.Live);

    public string Summary
    {
        get
        {
            if (!Accepted)
                return Errors.Count == 0 ? "Diffusion refusée." : string.Join(" ", Errors);

            if (!TvIsLive)
                return "EECC TV n'est pas en direct.";

            var externals = Destinations.Where(destination => destination.Kind == DestinationKind.External).ToList();
            if (externals.Count == 0)
                return "EECC TV seule — en direct.";

            var live = externals.Count(destination => destination.State == DestinationRunState.Live);
            var failed = externals.Count(destination => destination.State == DestinationRunState.Error);
            return $"EECC TV en direct — {live} externe(s) en cours, {failed} en erreur.";
        }
    }

    public static BroadcastSnapshot Idle() =>
        new(false, Array.Empty<DestinationStatus>(), Array.Empty<string>(), Array.Empty<string>());

    public static BroadcastSnapshot Rejected(ValidationResult validation) =>
        new(false, Array.Empty<DestinationStatus>(), validation.Errors, validation.Warnings);

    public static BroadcastSnapshot TvFailed(string error) =>
        new(false, new[] { DestinationStatus.Tv(DestinationRunState.Error, null, error) }, new[] { error }, Array.Empty<string>());

    public static BroadcastSnapshot Running(
        IReadOnlyList<DestinationStatus> destinations,
        IReadOnlyList<string> warnings) =>
        new(true, destinations, Array.Empty<string>(), warnings);

    public BroadcastSnapshot Replace(DestinationStatus status)
    {
        var next = Destinations.Select(item => item.Id == status.Id ? status : item).ToList();
        if (next.All(item => item.Id != status.Id))
            next.Add(status);
        return new BroadcastSnapshot(Accepted, next, Errors, Warnings);
    }

    public BroadcastSnapshot AsStopped()
    {
        var next = Destinations.Select(item =>
            item.State is DestinationRunState.Stopped or DestinationRunState.Error
                ? item
                : item.With(DestinationRunState.Stopped, item.Error)).ToList();
        return new BroadcastSnapshot(false, next, Errors, Warnings);
    }

    public override string ToString() => Summary;
}

public sealed class BroadcastSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly List<IExternalOutput> _outputs = new();
    private readonly RedactingLog _log;
    private ITvOutput? _tv;
    private BroadcastSnapshot _latest = BroadcastSnapshot.Idle();
    private int _active;

    public BroadcastSession(RedactingLog? log = null)
    {
        _log = log ?? new RedactingLog();
    }

    public bool IsActive => Volatile.Read(ref _active) == 1;

    public RedactingLog Log => _log;

    public BroadcastSnapshot Latest
    {
        get
        {
            lock (_gate)
                return _latest;
        }
    }

    public async Task<BroadcastSnapshot> StartAsync(
        BroadcastPlan plan,
        ITvOutput tv,
        IExternalOutputFactory externals,
        Func<byte[]?> currentFrame,
        CancellationToken cancellationToken = default)
    {
        var secrets = SecretsOf(plan);
        var validation = BroadcastPlanValidator.Validate(plan);
        if (!validation.IsValid)
        {
            var rejected = BroadcastSnapshot.Rejected(validation);
            lock (_gate)
                _latest = rejected;
            _log.Info(rejected.Summary, secrets);
            return rejected;
        }

        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            return Latest;

        _tv = tv;
        TvOutputResult tvResult;
        try
        {
            tvResult = await tv.StartAsync(currentFrame(), cancellationToken);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _active, 0);
            var message = SecretRedactor.Redact(ex.Message, secrets);
            var failed = BroadcastSnapshot.TvFailed(message);
            lock (_gate)
                _latest = failed;
            _log.Info(message, secrets);
            return failed;
        }

        if (!tvResult.IsLive)
        {
            Interlocked.Exchange(ref _active, 0);
            try { await tv.StopAsync(); } catch { }
            var message = string.IsNullOrWhiteSpace(tvResult.Error)
                ? "EECC TV n'a pas démarré."
                : SecretRedactor.Redact(tvResult.Error, secrets);
            var failed = BroadcastSnapshot.TvFailed(message);
            lock (_gate)
                _latest = failed;
            _log.Info(message, secrets);
            return failed;
        }

        var statuses = new List<DestinationStatus>
        {
            DestinationStatus.Tv(DestinationRunState.Live, tvResult.PlaybackUrl, null)
        };
        Publish(statuses, validation.Warnings);

        foreach (var warning in validation.Warnings)
            _log.Info(warning, secrets);

        foreach (var destination in plan.Externals)
        {
            var output = externals.Create(destination);
            lock (_gate)
                _outputs.Add(output);
            output.StateChanged += (state, error) => OnExternalChanged(destination, state, error, secrets);

            OutputStart started;
            try
            {
                started = await output.StartAsync(currentFrame, cancellationToken);
            }
            catch (Exception ex)
            {
                var message = SecretRedactor.Redact(ex.Message, secrets);
                statuses.Add(DestinationStatus.External(destination, DestinationRunState.Error, message));
                _log.Info($"Destination {destination.DisplayName} en erreur.", secrets);
                continue;
            }

            var safeError = SecretRedactor.Redact(started.Error, secrets);
            statuses.Add(DestinationStatus.External(destination, started.State, safeError));
            Publish(statuses, validation.Warnings);
            _log.Info(
                started.State == DestinationRunState.Live
                    ? $"Destination {destination.DisplayName} en cours."
                    : $"Destination {destination.DisplayName} en erreur.",
                secrets);
        }

        Publish(statuses, validation.Warnings);
        var snapshot = Latest;
        _log.Info(snapshot.Summary, secrets);
        return snapshot;
    }

    private void Publish(List<DestinationStatus> statuses, IReadOnlyList<string> warnings)
    {
        lock (_gate)
        {
            var merged = statuses.Select(status =>
            {
                var newer = _latest.Destinations.FirstOrDefault(item => item.Id == status.Id);
                if (newer != null && newer.State == DestinationRunState.Error && status.State == DestinationRunState.Live)
                    return newer;
                return status;
            }).ToList();
            _latest = BroadcastSnapshot.Running(merged, warnings);
        }
    }

    public async Task<BroadcastSnapshot> StopAsync()
    {
        IExternalOutput[] outputs;
        lock (_gate)
            outputs = _outputs.ToArray();

        foreach (var output in outputs)
        {
            try { await output.StopAsync(); }
            catch { }
        }

        lock (_gate)
            _outputs.Clear();

        if (_tv != null)
        {
            try { await _tv.StopAsync(); }
            catch { }
        }

        Interlocked.Exchange(ref _active, 0);
        BroadcastSnapshot stopped;
        lock (_gate)
        {
            stopped = _latest.AsStopped();
            _latest = stopped;
        }

        _log.Info("Diffusion arrêtée.");
        return stopped;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private void OnExternalChanged(ExternalDestination destination, DestinationRunState state, string? error, string[] secrets)
    {
        var safe = SecretRedactor.Redact(error, secrets);
        lock (_gate)
        {
            _latest = _latest.Replace(DestinationStatus.External(destination, state, safe));
        }

        if (state == DestinationRunState.Error)
            _log.Info($"Destination {destination.DisplayName} isolée. EECC TV continue.", secrets);
    }

    private static string[] SecretsOf(BroadcastPlan plan) =>
        plan.Externals.Select(destination => destination.StreamKey.Reveal()).ToArray();
}

public static class FfmpegLocator
{
    public static string? Find()
    {
        var configured = Environment.GetEnvironmentVariable("EECC_FFMPEG");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var fileName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}

public sealed class FfmpegRtmpOutput : IExternalOutput
{
    private readonly ExternalDestination _destination;
    private readonly string[] _secrets;
    private readonly StringBuilder _stderr = new();
    private Process? _process;
    private string? _frameFile;
    private volatile bool _stopping;

    public FfmpegRtmpOutput(ExternalDestination destination)
    {
        _destination = destination;
        _secrets = new[] { destination.StreamKey.Reveal() };
    }

    public string DestinationId => _destination.Id;

    public event Action<DestinationRunState, string?>? StateChanged;

    public async Task<OutputStart> StartAsync(Func<byte[]?> currentFrame, CancellationToken cancellationToken)
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

        _frameFile = Path.Combine(Path.GetTempPath(), "eecc-frame-" + Guid.NewGuid().ToString("N") + ".jpg");
        File.WriteAllBytes(_frameFile, ProgramFrame(currentFrame));

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        AddArguments(startInfo, _frameFile);
        startInfo.ArgumentList.Add(url);

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("ffmpeg n'a pas démarré.");
        }
        catch (Exception ex)
        {
            DeleteFrame();
            return new OutputStart(DestinationRunState.Error, SecretRedactor.Redact(ex.Message, _secrets.Append(url)));
        }

        _process = process;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, eventArgs) => AppendStderr(eventArgs.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var deadline = DateTime.UtcNow.AddSeconds(2.5);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited || HasFatalStderr())
            {
                await StopProcessAsync(process);
                _process = null;
                DeleteFrame();
                return new OutputStart(DestinationRunState.Error, FailureMessage());
            }

            await Task.Delay(100, cancellationToken);
        }

        if (process.HasExited || HasFatalStderr())
        {
            await StopProcessAsync(process);
            _process = null;
            DeleteFrame();
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

        return new OutputStart(DestinationRunState.Live, null);
    }

    public async Task StopAsync()
    {
        _stopping = true;
        var process = _process;
        if (process == null)
            return;
        await StopProcessAsync(process);
        _process = null;
        DeleteFrame();
    }

    private static void AddArguments(ProcessStartInfo startInfo, string frameFile)
    {
        // L'image du programme est répétée. Le pipe d'entrée de Process ne livre pas les
        // octets à ffmpeg sur cette plateforme. Le fichier est l'image courante au démarrage.
        string[] args =
        {
            "-hide_banner", "-loglevel", "warning",
            "-loop", "1", "-framerate", "10", "-i", frameFile,
            "-f", "lavfi", "-i", "anullsrc=channel_layout=stereo:sample_rate=44100",
            "-map", "0:v:0", "-map", "1:a:0",
            "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency", "-pix_fmt", "yuv420p",
            "-g", "10", "-b:v", "250k",
            "-c:a", "aac", "-b:a", "64k", "-ar", "44100", "-ac", "2",
            "-flush_packets", "1",
            "-f", "flv"
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);
    }

    private static byte[] ProgramFrame(Func<byte[]?> currentFrame)
    {
        var frame = currentFrame() ?? EeccTvFrames.Slate;
        if (frame.Length < 4 || frame[0] != 0xFF || frame[1] != 0xD8)
            return EeccTvFrames.Slate;
        return frame;
    }

    private void DeleteFrame()
    {
        var path = _frameFile;
        _frameFile = null;
        if (string.IsNullOrWhiteSpace(path))
            return;
        try { if (File.Exists(path)) File.Delete(path); } catch { }
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
            || text.Contains("Could not find codec parameters", StringComparison.OrdinalIgnoreCase);
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

    private static async Task StopProcessAsync(Process process)
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
        }
    }
}

public sealed class FfmpegOutputFactory : IExternalOutputFactory
{
    public IExternalOutput Create(ExternalDestination destination) => new FfmpegRtmpOutput(destination);
}
