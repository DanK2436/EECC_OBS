namespace EeccStudio.Broadcast;

public enum BroadcastMode
{
    TvOnly = 1,
    TvPlusOne = 2,
    TvPlusThreeOrMore = 3
}

public enum ExternalPlatform
{
    YouTube,
    Facebook,
    TikTok,
    Custom
}

public enum DestinationKind
{
    EeccTv,
    External
}

public enum DestinationRunState
{
    Idle,
    Starting,
    Live,
    Error,
    Stopped
}

/// <summary>
/// Clé de flux tenue hors des journaux et de ToString.
/// </summary>
public sealed class StreamKey
{
    private readonly string _value;

    public StreamKey(string? value)
    {
        _value = value?.Trim() ?? string.Empty;
    }

    public bool IsEmpty => _value.Length == 0;

    public string Reveal() => _value;

    public override string ToString() => "***";
}

public sealed class TvDestination
{
    public string Id { get; init; } = "eecc-tv";

    public string DisplayName { get; init; } = "EECC TV";

    public int Port { get; init; } = 8089;

    public string AnnouncedHost { get; init; } = "127.0.0.1";
}

public sealed class ExternalDestination
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required ExternalPlatform Platform { get; init; }

    /// <summary>Adresse d'ingestion sans la clé de flux.</summary>
    public required string IngestUrl { get; init; }

    public required StreamKey StreamKey { get; init; }

    public override string ToString() => $"{DisplayName} ({Platform})";
}

public sealed class BroadcastPlan
{
    public required BroadcastMode Mode { get; init; }

    public required TvDestination Tv { get; init; }

    public required IReadOnlyList<ExternalDestination> Externals { get; init; }

    /// <summary>
    /// Faux par défaut : une adresse publique est refusée tant que l'opérateur ne la confirme pas.
    /// </summary>
    public bool AllowPublicIngest { get; init; }

    public override string ToString() =>
        $"{Mode} — EECC TV + {Externals.Count} externe(s)";
}

public static class BroadcastModeText
{
    public static string Label(BroadcastMode mode) => mode switch
    {
        BroadcastMode.TvOnly => "EECC TV seule",
        BroadcastMode.TvPlusOne => "EECC TV + une plateforme externe",
        BroadcastMode.TvPlusThreeOrMore => "EECC TV + trois plateformes externes ou plus",
        _ => "Mode inconnu"
    };

    public static string? PlatformWarning(ExternalPlatform platform) => platform switch
    {
        ExternalPlatform.YouTube =>
            "Le modèle d'adresse YouTube est public. Cette version n'a pas vérifié la diffusion avec un compte YouTube.",
        ExternalPlatform.Facebook =>
            "Le modèle d'adresse Facebook est public. Cette version n'a pas vérifié la diffusion avec un compte Facebook.",
        ExternalPlatform.TikTok =>
            "TikTok n'accepte un flux RTMP que pour un compte Live autorisé, avec l'adresse et la clé fournies par TikTok. Cette compatibilité n'est pas vérifiée ici.",
        _ => null
    };

    public static string? SuggestedIngest(ExternalPlatform platform) => platform switch
    {
        ExternalPlatform.YouTube => "rtmp://a.rtmp.youtube.com/live2",
        ExternalPlatform.Facebook => "rtmps://live-api-s.facebook.com:443/rtmp",
        _ => null
    };
}
