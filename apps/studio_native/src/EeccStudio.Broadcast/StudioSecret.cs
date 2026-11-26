using System.Security.Cryptography;

namespace EeccStudio.Broadcast;

/// <summary>
/// Secret local de la régie. Il n'est pas dans l'application EECC TV.
/// </summary>
public static class StudioSecret
{
    private static string? _current;

    public static string Current => _current ??= Load();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EECC_Studio",
        "studio.secret");

    private static string Load()
    {
        foreach (var candidate in BundledLocations())
        {
            if (!File.Exists(candidate))
                continue;
            var bundled = File.ReadAllText(candidate).Trim();
            if (bundled.Length >= 16)
                return bundled;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        if (File.Exists(Path))
        {
            var existing = File.ReadAllText(Path).Trim();
            if (existing.Length >= 16)
                return existing;
        }

        var created = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        File.WriteAllText(Path, created);
        return created;
    }

    private static IEnumerable<string> BundledLocations()
    {
        yield return System.IO.Path.Combine(AppContext.BaseDirectory, "studio.secret");
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 6 && cursor != null; depth++)
        {
            yield return System.IO.Path.Combine(cursor.FullName, "installer", "studio.secret");
            cursor = cursor.Parent;
        }
    }
}
