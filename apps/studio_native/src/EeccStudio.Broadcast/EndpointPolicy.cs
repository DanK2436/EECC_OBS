using System.Net;
using System.Net.Sockets;

namespace EeccStudio.Broadcast;

public static class EndpointPolicy
{
    public static bool IsLocalOrPrivate(Uri uri)
    {
        var host = uri.Host.Trim().Trim('[', ']');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var address))
            return false;

        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var bytes = address.GetAddressBytes();
        if (bytes[0] == 10)
            return true;
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;
        if (bytes[0] == 169 && bytes[1] == 254)
            return true;
        return false;
    }
}

public static class PublishUrl
{
    public static bool TryParseIngest(string? raw, out Uri? uri, out string error)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "L'adresse d'ingestion est absente.";
            return false;
        }

        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var parsed))
        {
            error = "L'adresse d'ingestion n'est pas une URL absolue.";
            return false;
        }

        if (parsed.Scheme is not ("rtmp" or "rtmps"))
        {
            error = "Une destination externe accepte seulement une adresse rtmp ou rtmps.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsed.Host))
        {
            error = "L'adresse d'ingestion n'a pas d'hôte.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            error = "L'adresse ne doit pas contenir d'identifiant. Saisissez la clé dans le champ prévu.";
            return false;
        }

        uri = parsed;
        error = string.Empty;
        return true;
    }

    public static string Build(ExternalDestination destination)
    {
        if (!TryParseIngest(destination.IngestUrl, out var uri, out var error) || uri == null)
            throw new InvalidOperationException(error);

        var key = destination.StreamKey.Reveal().Trim().Trim('/');
        var path = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        if (path.EndsWith("/" + key, StringComparison.Ordinal))
            return path;

        return path + "/" + key;
    }

    public static string Redacted(string ingestUrl)
    {
        if (!TryParseIngest(ingestUrl, out var uri, out _) || uri == null)
            return "rtmp://***";

        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
        return $"{uri.Scheme}://{uri.Host}{port}{uri.AbsolutePath.TrimEnd('/')}/***";
    }
}
