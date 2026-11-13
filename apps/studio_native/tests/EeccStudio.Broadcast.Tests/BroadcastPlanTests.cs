using Xunit;

namespace EeccStudio.Broadcast.Tests;

public class BroadcastPlanTests
{
    private const string Key = "clef-test-locale";

    [Fact]
    public void Mode1_AcceptsTvOnly()
    {
        var result = BroadcastPlanValidator.Validate(Plan(BroadcastMode.TvOnly));
        Assert.True(result.IsValid, result.Summary);
    }

    [Fact]
    public void Mode1_RejectsAnExternalDestination()
    {
        var result = BroadcastPlanValidator.Validate(Plan(BroadcastMode.TvOnly, Local("a")));
        Assert.False(result.IsValid);
        Assert.Contains("EECC TV", result.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public void Mode2_RequiresExactlyOneExternal(int count)
    {
        var destinations = Enumerable.Range(0, count).Select(index => Local("d" + index)).ToArray();
        var result = BroadcastPlanValidator.Validate(Plan(BroadcastMode.TvPlusOne, destinations));
        if (count == 1)
            Assert.True(result.IsValid, result.Summary);
        else
            Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Mode3_RequiresAtLeastThreeExternals(int count)
    {
        var destinations = Enumerable.Range(0, count).Select(index => Local("d" + index)).ToArray();
        var result = BroadcastPlanValidator.Validate(Plan(BroadcastMode.TvPlusThreeOrMore, destinations));
        if (count >= 3)
            Assert.True(result.IsValid, result.Summary);
        else
            Assert.False(result.IsValid);
    }

    [Fact]
    public void MissingUrlOrKey_IsRejected()
    {
        var missingKey = new ExternalDestination
        {
            Id = "sans-cle",
            DisplayName = "Locale",
            Platform = ExternalPlatform.Custom,
            IngestUrl = "rtmp://127.0.0.1:1935/live",
            StreamKey = new StreamKey("  ")
        };
        var missingUrl = new ExternalDestination
        {
            Id = "sans-url",
            DisplayName = "Locale",
            Platform = ExternalPlatform.Custom,
            IngestUrl = "",
            StreamKey = new StreamKey(Key)
        };

        Assert.False(BroadcastPlanValidator.Validate(Plan(BroadcastMode.TvPlusOne, missingKey)).IsValid);
        var urlResult = BroadcastPlanValidator.Validate(Plan(BroadcastMode.TvPlusOne, missingUrl));
        Assert.False(urlResult.IsValid);
        Assert.DoesNotContain(Key, urlResult.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyInsideUrl_IsRejectedWithoutEchoingIt()
    {
        var destination = new ExternalDestination
        {
            Id = "cle-dans-url",
            DisplayName = "Locale",
            Platform = ExternalPlatform.Custom,
            IngestUrl = "rtmp://127.0.0.1:1935/live/" + Key,
            StreamKey = new StreamKey(Key)
        };
        var result = BroadcastPlanValidator.Validate(Plan(BroadcastMode.TvPlusOne, destination));
        Assert.False(result.IsValid);
        Assert.DoesNotContain(Key, result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicAddress_IsBlockedUntilTheOperatorConfirms()
    {
        var youtube = new ExternalDestination
        {
            Id = "yt",
            DisplayName = "YouTube",
            Platform = ExternalPlatform.YouTube,
            IngestUrl = "rtmp://a.rtmp.youtube.com/live2",
            StreamKey = new StreamKey(Key)
        };
        var blocked = BroadcastPlanValidator.Validate(Plan(BroadcastMode.TvPlusOne, youtube));
        Assert.False(blocked.IsValid);
        Assert.Contains("hors du réseau local", blocked.Summary, StringComparison.Ordinal);

        var allowed = BroadcastPlanValidator.Validate(new BroadcastPlan
        {
            Mode = BroadcastMode.TvPlusOne,
            AllowPublicIngest = true,
            Tv = new TvDestination(),
            Externals = new[] { youtube }
        });
        Assert.True(allowed.IsValid, allowed.Summary);
        Assert.Contains("YouTube", string.Join(" ", allowed.Warnings), StringComparison.Ordinal);
        Assert.DoesNotContain(Key, allowed.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TikTok_StaysAWarning_NotAProvenCompatibility()
    {
        var result = BroadcastPlanValidator.Validate(Plan(
            BroadcastMode.TvPlusOne,
            Local("tiktok", ExternalPlatform.TikTok)));
        Assert.True(result.IsValid, result.Summary);
        Assert.Contains("TikTok", string.Join(" ", result.Warnings), StringComparison.Ordinal);
        Assert.Contains("pas vérifiée", string.Join(" ", result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void RedactorAndProfile_OmitTheKey()
    {
        const string second = "autre-clef-locale";
        var text = SecretRedactor.Redact(
            "envoi rtmp://127.0.0.1/live/" + Key + " et " + second,
            new[] { Key, second });
        Assert.DoesNotContain(Key, text, StringComparison.Ordinal);
        Assert.DoesNotContain(second, text, StringComparison.Ordinal);

        var directory = Path.Combine(Path.GetTempPath(), "eecc-profile-" + Guid.NewGuid().ToString("N"));
        var plan = Plan(BroadcastMode.TvPlusOne, Local("profil"));
        var store = new BroadcastProfileStore(directory);
        store.Save(plan);
        var json = File.ReadAllText(store.ProfilePath);
        Assert.DoesNotContain(Key, json, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1", json, StringComparison.Ordinal);

        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.Equal(Key, loaded!.Externals[0].StreamKey.Reveal());
        Assert.Equal("***", loaded.Externals[0].StreamKey.ToString());
        Assert.DoesNotContain(Key, loaded.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalFailure_DoesNotStopTv_AndStopCleansUp()
    {
        var tv = new ScriptedTv();
        var good = new ScriptedOutput("bon", fail: false);
        var bad = new ScriptedOutput("mauvais", fail: true);
        var throwing = new ScriptedOutput("arret", fail: false) { ThrowOnStop = true };
        var session = new BroadcastSession();
        var plan = Plan(
            BroadcastMode.TvPlusThreeOrMore,
            Local("bon"),
            Local("mauvais"),
            Local("arret"));

        var snapshot = await session.StartAsync(
            plan,
            tv,
            new MapFactory(new Dictionary<string, IExternalOutput>
            {
                ["bon"] = good,
                ["mauvais"] = bad,
                ["arret"] = throwing
            }),
            () => EeccTvFrames.Slate);

        Assert.True(snapshot.TvIsLive);
        Assert.Equal(1, tv.Starts);
        Assert.True(good.Started);
        Assert.True(bad.Started);
        Assert.Equal(DestinationRunState.Error, snapshot.Destinations.Single(item => item.Id == "mauvais").State);
        Assert.DoesNotContain(Key, snapshot.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, session.Log.Dump(), StringComparison.Ordinal);

        bad.Raise(DestinationRunState.Error, "panne " + Key);
        Assert.True(session.Latest.TvIsLive);
        Assert.DoesNotContain(Key, session.Latest.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Key, session.Log.Dump(), StringComparison.Ordinal);

        var stopped = await session.StopAsync();
        Assert.False(stopped.TvIsLive);
        Assert.False(tv.Live);
        Assert.True(good.Stopped);
        Assert.True(bad.Stopped);
        Assert.True(throwing.Stopped);
        Assert.False(session.IsActive);
    }

    [Fact]
    public async Task TvFailure_DoesNotStartExternals()
    {
        var external = new ScriptedOutput("seul", fail: false);
        var session = new BroadcastSession();
        var snapshot = await session.StartAsync(
            Plan(BroadcastMode.TvPlusOne, Local("seul")),
            new ScriptedTv { Fail = true },
            new MapFactory(new Dictionary<string, IExternalOutput> { ["seul"] = external }),
            () => EeccTvFrames.Slate);

        Assert.False(snapshot.TvIsLive);
        Assert.False(external.Started);
        Assert.False(session.IsActive);
    }

    private static BroadcastPlan Plan(BroadcastMode mode, params ExternalDestination[] externals) => new()
    {
        Mode = mode,
        Tv = new TvDestination { Port = 8089, AnnouncedHost = "127.0.0.1" },
        Externals = externals
    };

    private static ExternalDestination Local(string id, ExternalPlatform platform = ExternalPlatform.Custom) => new()
    {
        Id = id,
        DisplayName = id,
        Platform = platform,
        IngestUrl = "rtmp://127.0.0.1:1935/live",
        StreamKey = new StreamKey(Key)
    };

    private sealed class ScriptedTv : ITvOutput
    {
        public bool Fail { get; init; }
        public int Starts { get; private set; }
        public bool Live { get; private set; }

        public Task<TvOutputResult> StartAsync(byte[]? programFrame, CancellationToken cancellationToken)
        {
            Starts++;
            if (Fail)
                return Task.FromResult(new TvOutputResult(false, null, "TV indisponible"));
            Live = true;
            return Task.FromResult(new TvOutputResult(true, "http://127.0.0.1:9/live/stream", null));
        }

        public Task StopAsync()
        {
            Live = false;
            return Task.CompletedTask;
        }
    }

    private sealed class ScriptedOutput : IExternalOutput
    {
        private readonly bool _fail;

        public ScriptedOutput(string id, bool fail)
        {
            DestinationId = id;
            _fail = fail;
        }

        public string DestinationId { get; }
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }
        public bool ThrowOnStop { get; init; }
        public event Action<DestinationRunState, string?>? StateChanged;

        public Task<OutputStart> StartAsync(Func<byte[]?> currentFrame, CancellationToken cancellationToken)
        {
            Started = true;
            return Task.FromResult(_fail
                ? new OutputStart(DestinationRunState.Error, "refus test")
                : new OutputStart(DestinationRunState.Live, null));
        }

        public Task StopAsync()
        {
            Stopped = true;
            if (ThrowOnStop)
                throw new InvalidOperationException("nettoyage interrompu");
            return Task.CompletedTask;
        }

        public void Raise(DestinationRunState state, string error) => StateChanged?.Invoke(state, error);
    }

    private sealed class MapFactory : IExternalOutputFactory
    {
        private readonly IReadOnlyDictionary<string, IExternalOutput> _outputs;

        public MapFactory(IReadOnlyDictionary<string, IExternalOutput> outputs) => _outputs = outputs;

        public IExternalOutput Create(ExternalDestination destination) => _outputs[destination.Id];
    }
}
