using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EeccStudio.Broadcast;

public static class SecretRedactor
{
    public static string Redact(string? text, IEnumerable<string?> secrets)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var result = text;
        foreach (var secret in secrets)
        {
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 3)
                continue;
            result = result.Replace(secret, "***", StringComparison.Ordinal);
        }

        return result;
    }
}

public sealed class RedactingLog
{
    private readonly List<string> _lines = new();
    private readonly object _gate = new();

    public void Info(string message, params string?[] secrets)
    {
        var line = SecretRedactor.Redact(message, secrets);
        lock (_gate)
            _lines.Add(line);
    }

    public string Dump()
    {
        lock (_gate)
            return string.Join('\n', _lines);
    }
}

public interface ISecretStore
{
    void Save(string destinationId, string secret);

    string? Load(string destinationId);

    void DeleteAll();
}

public static class SecretStoreFactory
{
    public static ISecretStore Create(string directory)
    {
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows())
            return new DpapiSecretStore(directory);
        return new FileModeSecretStore(directory);
    }
}

/// <summary>Protection DPAPI du compte Windows. Le fichier ne contient pas la clé en clair.</summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] Entropy = "EECC.Studio.Broadcast.v1"u8.ToArray();
    private readonly string _directory;

    public DpapiSecretStore(string directory)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI est disponible sur Windows.");
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public void Save(string destinationId, string secret)
    {
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(secret),
            Entropy,
            DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PathFor(destinationId), protectedBytes);
    }

    public string? Load(string destinationId)
    {
        var path = PathFor(destinationId);
        if (!File.Exists(path))
            return null;
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }

    public void DeleteAll()
    {
        if (!Directory.Exists(_directory))
            return;
        foreach (var file in Directory.GetFiles(_directory, "*.bin"))
            File.Delete(file);
    }

    private string PathFor(string destinationId) =>
        Path.Combine(_directory, Safe(destinationId) + ".bin");

    private static string Safe(string destinationId)
    {
        var chars = destinationId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }
}

/// <summary>
/// Secours hors Windows : fichier lisible seulement par l'utilisateur. Les tests locaux s'en servent.
/// Le profil public ne reçoit jamais la clé.
/// </summary>
public sealed class FileModeSecretStore : ISecretStore
{
    private readonly string _directory;

    public FileModeSecretStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public void Save(string destinationId, string secret)
    {
        var path = Path.Combine(_directory, Safe(destinationId) + ".secret");
        File.WriteAllText(path, secret);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public string? Load(string destinationId)
    {
        var path = Path.Combine(_directory, Safe(destinationId) + ".secret");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void DeleteAll()
    {
        if (!Directory.Exists(_directory))
            return;
        foreach (var file in Directory.GetFiles(_directory, "*.secret"))
            File.Delete(file);
    }

    private static string Safe(string destinationId)
    {
        var chars = destinationId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }
}

public sealed class BroadcastProfileStore
{
    private readonly string _profilePath;
    private readonly ISecretStore _secrets;

    public BroadcastProfileStore(string directory, ISecretStore? secrets = null)
    {
        Directory.CreateDirectory(directory);
        _profilePath = Path.Combine(directory, "broadcast-profile.json");
        _secrets = secrets ?? SecretStoreFactory.Create(Path.Combine(directory, "keys"));
    }

    public string ProfilePath => _profilePath;

    public void Save(BroadcastPlan plan)
    {
        var dto = new ProfileDto
        {
            Mode = plan.Mode.ToString(),
            AllowPublicIngest = plan.AllowPublicIngest,
            TvPort = plan.Tv.Port,
            TvHost = plan.Tv.AnnouncedHost,
            Externals = plan.Externals.Select(destination => new ExternalDto
            {
                Id = destination.Id,
                DisplayName = destination.DisplayName,
                Platform = destination.Platform.ToString(),
                IngestUrl = destination.IngestUrl
            }).ToList()
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions());
        foreach (var destination in plan.Externals)
        {
            var key = destination.StreamKey.Reveal();
            if (!destination.StreamKey.IsEmpty && json.Contains(key, StringComparison.Ordinal))
                throw new InvalidOperationException("Le profil public ne peut pas contenir une clé de flux.");
        }

        File.WriteAllText(_profilePath, json);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_profilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        foreach (var destination in plan.Externals)
            _secrets.Save(destination.Id, destination.StreamKey.Reveal());
    }

    public BroadcastPlan? Load()
    {
        if (!File.Exists(_profilePath))
            return null;

        var dto = JsonSerializer.Deserialize<ProfileDto>(File.ReadAllText(_profilePath), JsonOptions());
        if (dto == null || !Enum.TryParse<BroadcastMode>(dto.Mode, out var mode))
            return null;

        var externals = new List<ExternalDestination>();
        foreach (var item in dto.Externals ?? new List<ExternalDto>())
        {
            if (!Enum.TryParse<ExternalPlatform>(item.Platform, out var platform))
                platform = ExternalPlatform.Custom;
            externals.Add(new ExternalDestination
            {
                Id = item.Id ?? Guid.NewGuid().ToString("N"),
                DisplayName = item.DisplayName ?? "Destination",
                Platform = platform,
                IngestUrl = item.IngestUrl ?? string.Empty,
                StreamKey = new StreamKey(_secrets.Load(item.Id ?? string.Empty))
            });
        }

        return new BroadcastPlan
        {
            Mode = mode,
            AllowPublicIngest = dto.AllowPublicIngest,
            Tv = new TvDestination
            {
                Port = dto.TvPort,
                AnnouncedHost = dto.TvHost ?? "127.0.0.1"
            },
            Externals = externals
        };
    }

    public static string DefaultDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(root, "EECC_Studio");
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class ProfileDto
    {
        public string? Mode { get; set; }
        public bool AllowPublicIngest { get; set; }
        public int TvPort { get; set; }
        public string? TvHost { get; set; }
        public List<ExternalDto>? Externals { get; set; }
    }

    private sealed class ExternalDto
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? Platform { get; set; }
        public string? IngestUrl { get; set; }
    }
}
