using System.Net;
using System.Text.Json;
using EeccStudio.Core.Pairing;

namespace EeccStudio.Core.Media;

public sealed class StudioLinkController : IAsyncDisposable
{
    public const int SignalingPort = 8091;

    private readonly object _gate = new();
    private SignalingServer? _server;
    private System.Security.Cryptography.X509Certificates.X509Certificate2? _certificate;
    private PairingTicket? _ticket;
    private bool _consumed;
    private LanMediaSession? _media;
    private readonly List<(string Candidate, string? Mid, ushort Line)> _earlyIce = new();
    private int? _battery;
    private bool _phoneMuted;
    private string _tally = "standby";

    public event Action<string>? StatusChanged;
    public event Action<DecodedVideoFrame>? VideoFrame;
    public event Action<short[]>? PcmDecoded;

    public int? ListeningPort => _server?.Port;
    public string? ActiveLanIp { get; private set; }
    public string? CurrentQrPayload { get; private set; }
    public DateTimeOffset? QrExpiresAt => _ticket?.ExpiresAt;
    public bool HasAuthenticatedPhone => _server?.HasAuthenticatedClient == true;
    public string Tally => _tally;

    public string MeasuredSummary
    {
        get
        {
            var media = _media;
            var parts = new List<string>
            {
                "ICE " + (media?.IceState ?? "absent"),
                media == null || media.VideoFrames == 0
                    ? "aucune image décodée"
                    : media.FrameWidth + "x" + media.FrameHeight + " (" + media.VideoFrames + " images)",
            };
            if (media?.MeasuredFps is double fps) parts.Add(fps.ToString("0.0") + " img/s mesurées");
            else parts.Add("cadence non mesurée");
            if (media?.MeasuredVideoKbps is double kbps) parts.Add(kbps.ToString("0") + " kbps vidéo mesurés");
            else parts.Add("débit vidéo non mesuré");
            parts.Add(media == null || media.AudioPackets == 0
                ? "aucun audio décodé"
                : media.AudioPackets + " paquets audio");
            parts.Add(_battery is int level ? "batterie " + level + " %" : "batterie non reçue");
            parts.Add(media?.PlaybackMuted == true ? "lecture locale coupée" : "lecture locale non coupée");
            parts.Add(_phoneMuted ? "micro téléphone coupé" : "micro téléphone non signalé coupé");
            parts.Add("tally " + _tally);
            return string.Join(" · ", parts);
        }
    }

    public async Task EnsureStartedAsync()
    {
        if (_server != null) return;
        _certificate = StudioCertificate.Create();
        var server = new SignalingServer(Authenticate, OnMessageAsync);
        server.StatusChanged += text => StatusChanged?.Invoke(text);
        await server.StartAsync(_certificate, SignalingPort).ConfigureAwait(false);
        _server = server;
        StatusChanged?.Invoke("Signalisation TLS en écoute sur le port " + server.Port);
    }

    public string? IssueQr(string ip)
    {
        if (_server == null || _certificate == null) return "La signalisation n'est pas démarrée.";
        if (!PairingTicket.IsAcceptableIpv4(ip)) return "Adresse IPv4 refusée.";
        var known = LanAddresses.List();
        if (known.Count == 0) return "Aucune adresse IPv4 locale joignable. Branchez le réseau de la régie.";
        if (!known.Any(item => item.Ip == ip)) return "Cette adresse n'est pas une interface locale de ce PC.";

        lock (_gate)
        {
            _ticket = PairingTicket.Issue(ip, _server.Port, StudioCertificate.Fingerprint(_certificate));
            _consumed = false;
            ActiveLanIp = ip;
            CurrentQrPayload = _ticket.ToQrJson();
        }
        StatusChanged?.Invoke("QR généré pour " + ip + ":" + _server.Port + ", valable 3 minutes");
        return null;
    }

    public bool TrySetTally(string state)
    {
        if (state is not ("preview" or "program" or "standby")) return false;
        _tally = state;
        if (_server == null || !_server.HasAuthenticatedClient)
        {
            StatusChanged?.Invoke("Tally non envoyé : aucun téléphone accepté");
            return false;
        }
        _ = _server.SendAsync("{\"type\":\"tally\",\"state\":\"" + state + "\"}");
        StatusChanged?.Invoke("Tally envoyé : " + state);
        return true;
    }

    public void SetPlaybackMuted(bool muted) => _media?.SetPlaybackMuted(muted);

    private string? Authenticate(string token)
    {
        lock (_gate)
        {
            var reason = PairingTicket.Evaluate(_ticket, token, DateTimeOffset.UtcNow, _consumed);
            if (reason == null) _consumed = true;
            return reason;
        }
    }

    private async Task OnMessageAsync(JsonElement root)
    {
        var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        try
        {
            if (type == "offer" && root.TryGetProperty("sdp", out var sdpEl))
            {
                await AcceptOfferAsync(sdpEl.GetString() ?? "").ConfigureAwait(false);
            }
            else if (type == "ice")
            {
                var candidate = root.TryGetProperty("candidate", out var c) ? c.GetString() : null;
                var mid = root.TryGetProperty("sdpMid", out var m) ? m.GetString() : null;
                ushort line = 0;
                if (root.TryGetProperty("sdpMLineIndex", out var lineEl) && lineEl.ValueKind == JsonValueKind.Number)
                    line = lineEl.GetUInt16();
                AddIce(candidate, mid, line);
            }
            else if (type == "telemetry" && root.TryGetProperty("battery", out var batteryEl) && batteryEl.TryGetInt32(out var level) && level is >= 0 and <= 100)
            {
                _battery = level;
                StatusChanged?.Invoke("Batterie téléphone " + level + " %");
            }
            else if (type == "audio" && root.TryGetProperty("muted", out var mutedEl) && mutedEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                _phoneMuted = mutedEl.GetBoolean();
                StatusChanged?.Invoke(_phoneMuted ? "Micro du téléphone coupé" : "Micro du téléphone ouvert");
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Message média refusé (" + ex.GetType().Name + ")");
        }
    }

    private async Task AcceptOfferAsync(string sdp)
    {
        if (string.IsNullOrWhiteSpace(sdp))
        {
            StatusChanged?.Invoke("Offre vidéo vide");
            return;
        }
        if (!IPAddress.TryParse(ActiveLanIp, out var bind))
        {
            StatusChanged?.Invoke("Adresse d'appairage absente");
            return;
        }

        ReplaceMedia(new LanMediaSession(bind));
        var answer = await _media!.AcceptOfferAsync(sdp).ConfigureAwait(false);
        List<(string Candidate, string? Mid, ushort Line)> queued;
        lock (_gate)
        {
            queued = _earlyIce.ToList();
            _earlyIce.Clear();
        }
        foreach (var ice in queued) _media.AddIce(ice.Candidate, ice.Mid, ice.Line);

        var sent = _server != null && await _server.SendAsync(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "answer",
            ["sdp"] = answer,
        })).ConfigureAwait(false);
        StatusChanged?.Invoke(sent ? "Réponse média envoyée" : "Réponse média non envoyée");
        if (_server?.HasAuthenticatedClient == true)
            await _server.SendAsync("{\"type\":\"tally\",\"state\":\"" + _tally + "\"}").ConfigureAwait(false);
    }

    private void AddIce(string? candidate, string? mid, ushort line)
    {
        var media = _media;
        if (media == null)
        {
            lock (_gate) _earlyIce.Add((candidate ?? "", mid, line));
            return;
        }
        media.AddIce(candidate, mid, line);
    }

    private void ReplaceMedia(LanMediaSession session)
    {
        var previous = _media;
        _media = session;
        session.StatusChanged += text => StatusChanged?.Invoke(text);
        session.VideoFrameDecoded += frame => VideoFrame?.Invoke(frame);
        session.PcmDecoded += pcm => PcmDecoded?.Invoke(pcm);
        session.LocalIceCandidateJson += json =>
        {
            if (_server != null) _ = _server.SendAsync(json);
        };
        previous?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _media?.Dispose();
        _media = null;
        if (_server != null) await _server.DisposeAsync().ConfigureAwait(false);
        _server = null;
        _certificate?.Dispose();
        _certificate = null;
        CurrentQrPayload = null;
    }
}
