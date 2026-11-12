using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EeccStudio.Core.Pairing;

public sealed class PairingTicket
{
    public const int ProtocolVersion = 1;
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(3);

    public string Ip { get; }
    public int Port { get; }
    public DateTimeOffset ExpiresAt { get; }
    public string Token { get; }
    public string CertSha256 { get; }

    public PairingTicket(string ip, int port, DateTimeOffset expiresAt, string token, string certSha256)
    {
        Ip = ip;
        Port = port;
        ExpiresAt = expiresAt;
        Token = token;
        CertSha256 = certSha256;
    }

    public static PairingTicket Issue(string ip, int port, string certSha256, TimeSpan? lifetime = null, DateTimeOffset? now = null)
    {
        var clock = now ?? DateTimeOffset.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        return new PairingTicket(ip, port, clock.Add(lifetime ?? DefaultLifetime), token, certSha256);
    }

    public string ToQrJson()
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["v"] = ProtocolVersion,
            ["ip"] = Ip,
            ["port"] = Port,
            ["exp"] = ExpiresAt.ToUnixTimeSeconds(),
            ["token"] = Token,
            ["fp"] = CertSha256,
        });
    }

    public override string ToString() => $"pair {Ip}:{Port} exp {ExpiresAt:o}";

    public static bool TryParse(string json, out PairingTicket? ticket, out string error)
    {
        ticket = null;
        error = "invalid";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!root.TryGetProperty("v", out var version))
                return false;
            if (version.ValueKind != JsonValueKind.Number || version.GetInt32() != ProtocolVersion)
            {
                error = "version";
                return false;
            }
            if (!root.TryGetProperty("ip", out var ipEl) || ipEl.ValueKind != JsonValueKind.String || !IsAcceptableIpv4(ipEl.GetString()))
                return false;
            if (!root.TryGetProperty("port", out var portEl) || portEl.ValueKind != JsonValueKind.Number || !portEl.TryGetInt32(out var port) || port is < 1 or > 65535)
                return false;
            if (!root.TryGetProperty("exp", out var expEl) || expEl.ValueKind != JsonValueKind.Number || !expEl.TryGetInt64(out var exp))
                return false;
            if (!root.TryGetProperty("token", out var tokenEl) || tokenEl.ValueKind != JsonValueKind.String || !IsToken(tokenEl.GetString()))
                return false;
            if (!root.TryGetProperty("fp", out var fpEl) || fpEl.ValueKind != JsonValueKind.String || !IsFingerprint(fpEl.GetString()))
                return false;

            ticket = new PairingTicket(
                ipEl.GetString()!,
                port,
                DateTimeOffset.FromUnixTimeSeconds(exp),
                tokenEl.GetString()!,
                fpEl.GetString()!);
            error = "";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? Evaluate(PairingTicket? ticket, string? presentedToken, DateTimeOffset now, bool alreadyUsed)
    {
        if (alreadyUsed) return "used";
        if (ticket == null) return "invalid";
        if (now > ticket.ExpiresAt) return "expired";
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(ticket.Token));
        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(presentedToken ?? ""));
        if (!CryptographicOperations.FixedTimeEquals(expected, presented)) return "invalid";
        return null;
    }

    public static bool IsToken(string? token) =>
        token != null && token.Length == 32 && token.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool IsFingerprint(string? fingerprint) =>
        fingerprint != null && fingerprint.Length == 64 && fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool IsAcceptableIpv4(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var address)) return false;
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        var text = address.ToString();
        if (text is "0.0.0.0" or "255.255.255.255" || IPAddress.IsLoopback(address)) return false;
        var parts = text.Split('.');
        return parts.Length == 4 && parts.All(p => byte.TryParse(p, out _));
    }
}
