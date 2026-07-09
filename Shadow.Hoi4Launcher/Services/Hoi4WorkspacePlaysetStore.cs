using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shadow.Hoi4Launcher.Models;

namespace Shadow.Hoi4Launcher.Services;

public sealed class Hoi4WorkspacePlaysetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public Hoi4WorkspacePlaysetStore(string workspaceDirectory)
    {
        WorkspaceDirectory = workspaceDirectory;
        PlaysetsDirectory = Path.Combine(workspaceDirectory, "playsets");
        ModsDirectory = Path.Combine(workspaceDirectory, "mods");
        ModIndexPath = Path.Combine(ModsDirectory, "index.json");
    }

    public string WorkspaceDirectory { get; }

    public string PlaysetsDirectory { get; }

    public string ModsDirectory { get; }

    public string ModIndexPath { get; }

    public static Hoi4WorkspacePlaysetStore CreateDefault()
    {
        return new Hoi4WorkspacePlaysetStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Posdaca",
            "Hoi4Workspace"));
    }

    public IReadOnlyList<Playset> LoadPlaysets()
    {
        Directory.CreateDirectory(PlaysetsDirectory);

        return Directory.EnumerateFiles(PlaysetsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(TryLoadPlayset)
            .Where(playset => playset is not null)
            .Select(playset => playset!)
            .OrderBy(playset => playset.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public void SavePlayset(Playset playset)
    {
        Directory.CreateDirectory(PlaysetsDirectory);
        Normalize(playset);

        var filePath = GetPlaysetPath(playset);
        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(playset, SerializerOptions));
        File.Move(temporaryPath, filePath, true);
        playset.StorageFilePath = filePath;
    }

    public void DeletePlayset(Playset playset)
    {
        var filePath = GetPlaysetPath(playset);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        playset.StorageFilePath = string.Empty;
    }

    public void SaveModIndex(IEnumerable<ModEntry> mods)
    {
        Directory.CreateDirectory(ModsDirectory);

        var index = new Hoi4WorkspaceModIndex(
            "1.0",
            DateTimeOffset.UtcNow,
            mods
                .GroupBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(mod => mod.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(mod => new Hoi4WorkspaceModIndexEntry(
                    Hoi4ModIdentity.GetStableId(mod),
                    mod.Id,
                    mod.Title,
                    mod.IsSteamWorkshopMod ? "steam" : "local",
                    mod.RemoteFileId,
                    mod.DescriptorPath,
                    mod.LauncherPath,
                    mod.ContentPath,
                    mod.Version,
                    Hoi4ModIdentity.GetIdentityKeys(mod)))
                .ToList());

        var temporaryPath = $"{ModIndexPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(index, SerializerOptions));
        File.Move(temporaryPath, ModIndexPath, true);
    }

    private Playset? TryLoadPlayset(string filePath)
    {
        try
        {
            var playset = JsonSerializer.Deserialize<Playset>(File.ReadAllText(filePath), SerializerOptions);
            if (playset is null)
            {
                return null;
            }

            playset.StorageFilePath = filePath;
            Normalize(playset, Path.GetFileNameWithoutExtension(filePath));
            return playset;
        }
        catch
        {
            return null;
        }
    }

    private string GetPlaysetPath(Playset playset)
    {
        var existingPath = playset.StorageFilePath;
        if (IsPlaysetStorePath(existingPath))
        {
            return existingPath;
        }

        var fileName = $"{SanitizeFileName(playset.Id)}.json";
        return Path.Combine(PlaysetsDirectory, fileName);
    }

    private bool IsPlaysetStorePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var directory = Path.GetFullPath(PlaysetsDirectory);
        var fullPath = Path.GetFullPath(filePath);
        var directoryPrefix = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)
               && string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static void Normalize(Playset playset, string? fallbackId = null)
    {
        if (string.IsNullOrWhiteSpace(playset.Id))
        {
            playset.Id = !string.IsNullOrWhiteSpace(fallbackId)
                ? fallbackId
                : Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(playset.Name))
        {
            playset.Name = playset.Id;
        }

        if (playset.ModIds.Count == 0 && playset.EnabledModIds.Count > 0)
        {
            playset.ModIds = playset.EnabledModIds.ToList();
        }

        if (string.IsNullOrWhiteSpace(playset.Source))
        {
            playset.Source = "Shadow";
        }

        if (playset.IsExternal)
        {
            playset.CanEdit = false;
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized) || sanitized is "." or "..")
        {
            sanitized = Hash(value);
        }

        return sanitized.Length <= 120 ? sanitized : $"{sanitized[..80]}_{Hash(value)[..12]}";
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}