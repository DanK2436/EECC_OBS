using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EeccStudio.Core.Pairing;

public readonly record struct LanCandidate(
    string Name,
    string Description,
    string Address,
    bool IsUp,
    bool IsLoopback);

/// <summary>
/// Choisit les adresses IPv4 privées réellement assignées. N'invente jamais d'adresse.
/// </summary>
public static class LanAddresses
{
    public static IReadOnlyList<string> ListAssigned()
    {
        var found = new List<LanCandidate>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPInterfaceProperties props;
            try
            {
                props = nic.GetIPProperties();
            }
            catch
            {
                continue;
            }

            foreach (var unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                found.Add(new LanCandidate(
                    nic.Name,
                    nic.Description,
                    unicast.Address.ToString(),
                    nic.OperationalStatus == OperationalStatus.Up,
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback));
            }
        }

        return SelectUsable(found);
    }

    public static IReadOnlyList<string> SelectUsable(IEnumerable<LanCandidate> candidates)
    {
        var result = new List<string>();
        foreach (var candidate in candidates)
        {
            if (!candidate.IsUp || candidate.IsLoopback)
                continue;
            if (IsVirtual(candidate.Name) || IsVirtual(candidate.Description))
                continue;
            if (!IsPrivateLanIpv4(candidate.Address))
                continue;
            if (!result.Contains(candidate.Address))
                result.Add(candidate.Address);
        }

        return result;
    }

    public static bool IsPrivateLanIpv4(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || !IPAddress.TryParse(address, out var ip))
            return false;
        if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes[0] == 10)
            return true;
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;
        return false;
    }

    private static bool IsVirtual(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var markers = new[]
        {
            "virtual", "vmware", "wsl", "hyper-v", "hyperv", "vethernet",
            "virtualbox", "docker", "tap", "tunnel", "bluetooth", "loopback"
        };
        foreach (var marker in markers)
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
