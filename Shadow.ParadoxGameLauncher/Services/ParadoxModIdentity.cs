using System.Security.Cryptography;
using System.Text;
using Shadow.ParadoxGameLauncher.Models;

namespace Shadow.ParadoxGameLauncher.Services;

public static class ParadoxModIdentity
{
    public static string GetStableId(ModEntry mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
        {
            return $"steam:{mod.RemoteFileId.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(mod.ContentPath))
        {
            return $"local-content:{Hash(NormalizePath(mod.ContentPath))}";
        }

        return $"local-descriptor:{Hash(NormalizePath(mod.DescriptorPath))}";
    }

    public static IReadOnlyDictionary<string, ModEntry> BuildLookup(IEnumerable<ModEntry> mods)
    {
        var lookup = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            Add(lookup, mod.Id, mod);
            Add(lookup, $"shadow:{mod.Id}", mod);
            Add(lookup, GetStableId(mod), mod);

            if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
            {
                Add(lookup, mod.RemoteFileId, mod);
                Add(lookup, $"steam:{mod.RemoteFileId.Trim()}", mod);
            }
        }

        return lookup;
    }

    public static bool ContainsModReference(ISet<string> references, ModEntry mod)
    {
        return references.Contains(mod.Id)
               || references.Contains($"shadow:{mod.Id}")
               || references.Contains(GetStableId(mod))
               || (!string.IsNullOrWhiteSpace(mod.RemoteFileId)
                   && (references.Contains(mod.RemoteFileId)
                       || references.Contains($"steam:{mod.RemoteFileId.Trim()}")));
    }

    public static IReadOnlyList<string> GetIdentityKeys(ModEntry mod)
    {
        var keys = new List<string> { GetStableId(mod), $"shadow:{mod.Id}" };

        if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
        {
            keys.Add($"steam:{mod.RemoteFileId.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(mod.ContentPath))
        {
            keys.Add($"local-content:{Hash(NormalizePath(mod.ContentPath))}");
        }

        if (!string.IsNullOrWhiteSpace(mod.DescriptorPath))
        {
            keys.Add($"local-descriptor:{Hash(NormalizePath(mod.DescriptorPath))}");
        }

        if (!string.IsNullOrWhiteSpace(mod.LauncherPath))
        {
            keys.Add($"launcher-path:{NormalizePath(mod.LauncherPath)}");
        }

        return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void Add(IDictionary<string, ModEntry> lookup, string key, ModEntry mod)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            lookup.TryAdd(key, mod);
        }
    }

    private static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(value)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToLowerInvariant();
        }
        catch
        {
            return value
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Trim()
                .TrimEnd(Path.DirectorySeparatorChar)
                .ToLowerInvariant();
        }
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}