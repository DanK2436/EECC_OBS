using System.Text.Json;

namespace EeccStudio.Core.Pairing;

/// <summary>
/// Jeton d'appairage à usage unique. Il n'est pas un secret permanent du Studio.
/// </summary>
public sealed class PairingTicket
{
    public const int TokenByteLength = 16;

    public string Token { get; }
    public string StudioName { get; }
    public string LanIpv4 { get; }
    public int Port { get; }
    public DateTimeOffset ExpiresAt { get; }
    public bool Consumed { get; internal set; }

    internal PairingTicket(string token, string studioName, string lanIpv4, int port, DateTimeOffset expiresAt)
    {
        Token = token;
        StudioName = studioName;
        LanIpv4 = lanIpv4;
        Port = port;
        ExpiresAt = expiresAt;
    }

    public string ToQrJson()
    {
        var payload = new Dictionary<string, object>
        {
            ["v"] = 1,
            ["studio_name"] = StudioName,
            ["studio_ip"] = LanIpv4,
            ["port"] = Port,
            ["token"] = Token,
            ["exp"] = ExpiresAt.ToUnixTimeSeconds(),
            ["path"] = "/camera/control",
            ["codec"] = "jpeg_stream",
            ["transport"] = "ws-jpeg",
        };
        return JsonSerializer.Serialize(payload);
    }
}
