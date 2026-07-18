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
            foreach (var key in GetIdentityKeys(mod))
            {
                Add(lookup, key, mod);
            }

            Add(lookup, mod.Id, mod);
            Add(lookup, $"shadow:{mod.Id}", mod);

            if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
            {
                Add(lookup, mod.RemoteFileId, mod);
            }

            if (!string.IsNullOrWhiteSpace(mod.LauncherPath))
            {
                Add(lookup, mod.LauncherPath, mod);
                Add(lookup, NormalizePath(mod.LauncherPath), mod);
                Add(lookup, NormalizeLauncherPath(mod.LauncherPath), mod);
            }

            if (!string.IsNullOrWhiteSpace(mod.DescriptorPath))
            {
                Add(lookup, mod.DescriptorPath, mod);
                Add(lookup, NormalizePath(mod.DescriptorPath), mod);
            }

            if (!string.IsNullOrWhiteSpace(mod.ContentPath))
            {
                Add(lookup, mod.ContentPath, mod);
                Add(lookup, NormalizePath(mod.ContentPath), mod);
            }
        }

        return lookup;
    }

    public static bool ContainsModReference(ISet<string> references, ModEntry mod)
    {
        if (references.Count == 0)
        {
            return false;
        }

        foreach (var key in GetIdentityKeys(mod)
                     .Append(mod.Id)
                     .Append($"shadow:{mod.Id}")
                     .Append(mod.RemoteFileId)
                     .Append(mod.LauncherPath)
                     .Append(NormalizeLauncherPath(mod.LauncherPath))
                     .Append(NormalizePath(mod.DescriptorPath))
                     .Append(NormalizePath(mod.ContentPath)))
        {
            if (!string.IsNullOrWhiteSpace(key) && references.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> GetIdentityKeys(ModEntry mod)
    {
        var keys = new List<string> { GetStableId(mod), $"shadow:{mod.Id}" };

        if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
        {
            keys.Add($"steam:{mod.RemoteFileId.Trim()}");
            keys.Add(mod.RemoteFileId.Trim());
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
            keys.Add($"launcher-path:{NormalizeLauncherPath(mod.LauncherPath)}");
        }

        return keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<ModEntry> Deduplicate(IEnumerable<ModEntry> mods)
    {
        return mods
            .GroupBy(GetStableId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(Score)
                .ThenBy(mod => mod.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(mod => mod.DescriptorPath, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(mod => mod.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        // Collapse duplicate separators while preserving UNC prefix.
        var isUnc = normalized.StartsWith(@"\\", StringComparison.Ordinal);
        var parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        normalized = string.Join(Path.DirectorySeparatorChar, parts);
        if (isUnc)
        {
            normalized = @"\\" + normalized;
        }
        else if (value.TrimStart().StartsWith('/') || (value.Length >= 2 && value[1] == ':'))
        {
            // Keep rooted POSIX-style or drive-rooted paths as rooted after slash normalization.
            if (Path.IsPathRooted(value.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
                && !Path.IsPathRooted(normalized)
                && value.TrimStart().StartsWith('/'))
            {
                normalized = Path.DirectorySeparatorChar + normalized;
            }
        }

        try
        {
            if (Path.IsPathRooted(normalized))
            {
                return Path.GetFullPath(normalized)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToLowerInvariant();
            }
        }
        catch
        {
            // Fall through to separator-normalized relative path.
        }

        return normalized
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToLowerInvariant();
    }

    public static string NormalizeLauncherPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Trim()
            .Replace('\\', '/')
            .Replace("//", "/")
            .TrimStart('/')
            .ToLowerInvariant();
    }

    private static int Score(ModEntry mod)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(mod.ContentPath)
            && (Directory.Exists(mod.ContentPath) || File.Exists(mod.ContentPath)))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
        {
            // Prefer the real workshop package descriptor over a generated local ugc_*.mod stub.
            if (string.Equals(Path.GetFileName(mod.DescriptorPath), "descriptor.mod", StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
            }
            else if (Path.GetFileName(mod.DescriptorPath)
                         .Contains($"ugc_{mod.RemoteFileId}", StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
        }
        else if (!string.IsNullOrWhiteSpace(mod.DescriptorPath)
                 && string.Equals(Path.GetExtension(mod.DescriptorPath), ".mod", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(mod.CoverImagePath) || mod.CoverImage is not null)
        {
            score += 5;
        }

        if (!string.IsNullOrWhiteSpace(mod.Version))
        {
            score += 1;
        }

        return score;
    }

    private static void Add(IDictionary<string, ModEntry> lookup, string? key, ModEntry mod)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            lookup.TryAdd(key, mod);
        }
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
