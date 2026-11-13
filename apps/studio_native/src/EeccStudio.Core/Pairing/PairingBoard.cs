using EeccStudio.Core.Network;

namespace EeccStudio.Core.Pairing;

/// <summary>
/// Décide du QR affiché par la fenêtre Studio. Le jeton ne tourne qu'après un appairage validé,
/// une expiration, ou quand les 15 places sont prises.
/// </summary>
public sealed class PairingBoard
{
    public const string CapacityMessage =
        "Capacité atteinte : 15 téléphones. Aucun nouveau QR n'est affiché. Les caméras physiques ne libèrent pas ces places.";

    private PairingTicket? _ticket;
    private bool _replace;

    public void RequestReplacement() => _replace = true;

    public PairingDisplay Refresh(CameraServer server, string? lanIpv4)
    {
        var phones = server.PhoneCount;
        if (phones >= CameraServer.MaxPhoneConnections)
        {
            RevokeUnconsumed(server);
            return new PairingDisplay(null, CapacityMessage, false, phones);
        }

        if (string.IsNullOrWhiteSpace(lanIpv4) || !LanAddresses.IsPrivateLanIpv4(lanIpv4))
        {
            RevokeUnconsumed(server);
            return new PairingDisplay(null, "Aucune adresse IPv4 privée utilisable. Le QR n'est pas affiché.", false, phones);
        }

        if (_replace || _ticket == null || _ticket.Consumed || _ticket.ExpiresAt <= server.Clock() || _ticket.LanIpv4 != lanIpv4)
        {
            if (_ticket != null && !_ticket.Consumed)
                server.RevokePairingTicket(_ticket.Token);
            _ticket = server.IssuePairingTicket(server.StudioName, lanIpv4);
            _replace = false;
        }

        var left = _ticket.ExpiresAt - server.Clock();
        var message = $"Valable encore {(int)left.TotalMinutes:00}:{left.Seconds:00} · usage unique · {phones}/{CameraServer.MaxPhoneConnections} téléphones";
        return new PairingDisplay(_ticket.ToQrJson(), message, true, phones);
    }

    private void RevokeUnconsumed(CameraServer server)
    {
        if (_ticket != null && !_ticket.Consumed)
            server.RevokePairingTicket(_ticket.Token);
        _ticket = null;
    }
}

public sealed record PairingDisplay(string? QrJson, string Message, bool ShowsQr, int PhoneCount);

/// <summary>
/// Même règle que l'image du QR dans la fenêtre WPF : elle change seulement si le JSON change.
/// </summary>
public sealed class ShownQr
{
    public string? Json { get; private set; }
    public int Changes { get; private set; }

    public bool Apply(string? qrJson)
    {
        if (qrJson == Json)
            return false;
        Json = qrJson;
        Changes++;
        return true;
    }
}
