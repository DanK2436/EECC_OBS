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

    Task<OutputStart> StartAsync(
        Func<byte[]?> currentFrame,
        Func<int, byte[]> renderAudio,
        CancellationToken cancellationToken);

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
        Func<int, byte[]> renderAudio,
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
                started = await output.StartAsync(currentFrame, renderAudio, cancellationToken);
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

public sealed class FfmpegOutputFactory : IExternalOutputFactory
{
    public IExternalOutput Create(ExternalDestination destination) => new FfmpegRtmpOutput(destination);
}
