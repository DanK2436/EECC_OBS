using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EeccStudio.Core.Pairing;

public sealed record LanAddress(string Ip, string InterfaceName, bool IsPrivate)
{
    public string Label => IsPrivate
        ? $"{Ip} — {InterfaceName} (réseau privé)"
        : $"{Ip} — {InterfaceName} (adresse non privée)";
}

public static class LanAddresses
{
    public static IReadOnlyList<LanAddress> List()
    {
        var found = new List<LanAddress>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) continue;
                var ip = address.ToString();
                if (!PairingTicket.IsAcceptableIpv4(ip)) continue;
                found.Add(new LanAddress(ip, nic.Name, IsPrivate(address)));
            }
        }

        return found
            .GroupBy(item => item.Ip)
            .Select(group => group.First())
            .OrderByDescending(item => item.IsPrivate)
            .ThenBy(item => item.Ip, StringComparer.Ordinal)
            .ToList();
    }

    public static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4) return false;
        if (bytes[0] == 10) return true;
        if (bytes[0] == 192 && bytes[1] == 168) return true;
        if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) return true;
        return false;
    }
}
