using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Microsoft.Data.Sqlite;
using Shadow.Hoi4Launcher.Models;

namespace Shadow.Hoi4Launcher.Services;

public sealed class Hoi4LauncherService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly Hoi4LauncherConfiguration _configuration;

    public Hoi4LauncherService(Hoi4LauncherConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GameUserDirectory => _configuration.State.GameUserDirectory;

    public string SettingsPath => Path.Combine(GameUserDirectory, "settings.txt");

    public string DlcLoadPath => Path.Combine(GameUserDirectory, "dlc_load.json");

    public IReadOnlyList<ModEntry> DiscoverMods()
    {
        var descriptors = new List<string>();
        var localModDirectory = Path.Combine(GameUserDirectory, "mod");
        if (Directory.Exists(localModDirectory))
        {
            descriptors.AddRange(Directory.EnumerateFiles(localModDirectory, "*.mod", SearchOption.TopDirectoryOnly));
        }

        if (Directory.Exists(_configuration.State.WorkshopDirectory))
        {
            descriptors.AddRange(Directory.EnumerateFiles(_configuration.State.WorkshopDirectory, "*.mod",
                SearchOption.AllDirectories));
            descriptors.AddRange(Directory.EnumerateFiles(_configuration.State.WorkshopDirectory, "descriptor.mod",
                SearchOption.AllDirectories));
        }

        return descriptors
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ParseModDescriptor)
            .OrderBy(mod => mod.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<DlcEntry> DiscoverDlcs()
    {
        var gameDirectory = TryGetGameDirectory();
        if (gameDirectory is null)
        {
            return [];
        }

        var dlcDirectory = Path.Combine(gameDirectory, "dlc");
        if (!Directory.Exists(dlcDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(dlcDirectory, "*.dlc", SearchOption.AllDirectories)
            .Select(path => ParseDlcDescriptor(gameDirectory, path))
            .OrderBy(dlc => dlc.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<Playset> ImportParadoxPlaysets(IEnumerable<ModEntry> knownMods)
    {
        SQLitePCL.Batteries_V2.Init();
        var databasePath = GetParadoxLauncherDatabasePath();
        if (!File.Exists(databasePath))
        {
            return [];
        }

        var modMap = BuildParadoxModMap(knownMods);
        try
        {
            var importedPlaysets = new List<Playset>();
            using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
            connection.Open();

            if (!TableExists(connection, "playsets") || !TableExists(connection, "playsets_mods"))
            {
                return [];
            }

            var playsets = ReadParadoxPlaysets(connection);
            var playsetMods = ReadParadoxPlaysetMods(connection);
            var launcherModMap = TableExists(connection, "mods")
                ? ReadParadoxMods(connection)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var playset in playsets)
            {
                var enabledModIds = playsetMods
                    .Where(mod =>
                        mod.Enabled && string.Equals(mod.PlaysetId, playset.Id, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(mod => mod.Position)
                    .Select(mod => ResolveKnownModId(mod.ModId, launcherModMap, modMap))
                    .Where(modId => !string.IsNullOrWhiteSpace(modId))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                importedPlaysets.Add(new Playset
                {
                    Id = $"paradox:{playset.Id}",
                    Name = playset.Name,
                    ModIds = enabledModIds.ToList(),
                    EnabledModIds = enabledModIds,
                    Source = "Paradox Launcher",
                    IsExternal = true,
                    CanEdit = false,
                });
            }

            return importedPlaysets;
        }
        catch
        {
            return [];
        }
    }

    public GameSettings LoadGameSettings()
    {
        return GameSettings.Load(SettingsPath);
    }

    public void SaveGameSettings(GameSettings gameSettings)
    {
        gameSettings.Save(SettingsPath);
    }

    public void ApplyPlayset(Playset playset, IEnumerable<ModEntry> mods, IEnumerable<DlcEntry> dlcs)
    {
        var modList = mods.ToArray();
        var dlcList = dlcs.ToArray();
        var enabledMods = modList.Where(mod => mod.IsEnabled).ToArray();

        var enabledModPaths = enabledMods
            .Select(EnsureLauncherModDescriptor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var disabledDlcIds = dlcList
            .Where(dlc => !dlc.IsEnabled)
            .Select(dlc => dlc.LauncherPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Directory.CreateDirectory(GameUserDirectory);
        File.WriteAllText(
            DlcLoadPath,
            JsonSerializer.Serialize(new DlcLoadDocument
            {
                EnabledMods = enabledModPaths,
                DisabledDlcs = disabledDlcIds,
            }, SerializerOptions));

        _configuration.Save();
    }

    public Process StartGame()
    {
        if (!File.Exists(_configuration.State.GameExecutablePath))
        {
            throw new InvalidOperationException("请先选择 hoi4.exe 或 dowser.exe。");
        }

        var executablePath = _configuration.State.GameExecutablePath;
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
        };

        if (!string.IsNullOrWhiteSpace(_configuration.State.LaunchArguments))
        {
            foreach (var argument in SplitArguments(_configuration.State.LaunchArguments))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        return Process.Start(startInfo)
               ?? throw new InvalidOperationException("启动游戏进程失败。");
    }

    public void SaveConfiguration()
    {
        _configuration.Save();
    }

    public void OpenModLocation(ModEntry mod)
    {
        var targetPath = ResolveModContentPath(mod);
        if (File.Exists(targetPath))
        {
            targetPath = Path.GetDirectoryName(targetPath) ?? targetPath;
        }

        if (!Directory.Exists(targetPath))
        {
            targetPath = Path.GetDirectoryName(mod.DescriptorPath) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
        {
            throw new InvalidOperationException("未找到该 Mod 的本地文件位置。");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = targetPath,
            UseShellExecute = true,
        });
    }

    public void OpenWorkshopPage(ModEntry mod)
    {
        if (!mod.IsSteamWorkshopMod)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = mod.WorkshopUrl,
            UseShellExecute = true,
        });
    }

    private string GetParadoxLauncherDatabasePath()
    {
        return Path.Combine(GameUserDirectory, "launcher-v2.sqlite");
    }

    private string? TryGetGameDirectory()
    {
        if (File.Exists(_configuration.State.GameExecutablePath))
        {
            return Path.GetDirectoryName(_configuration.State.GameExecutablePath);
        }

        return null;
    }

    private ModEntry ParseModDescriptor(string descriptorPath)
    {
        var values = ReadClausewitzKeyValues(descriptorPath);
        if (!values.TryGetValue("name", out var title))
        {
            title = Path.GetFileNameWithoutExtension(descriptorPath);
        }

        values.TryGetValue("path", out var archivePath);
        values.TryGetValue("archive", out var archive);
        values.TryGetValue("remote_file_id", out var remoteFileId);
        values.TryGetValue("version", out var version);
        if (string.IsNullOrWhiteSpace(version))
        {
            values.TryGetValue("supported_version", out version);
        }

        values.TryGetValue("picture", out var picture);

        var resolvedArchivePath = !string.IsNullOrWhiteSpace(archivePath) ? archivePath : archive;
        var id = !string.IsNullOrWhiteSpace(remoteFileId)
            ? remoteFileId
            : descriptorPath.ToUpperInvariant();
        var launcherPath = GetLauncherModPath(descriptorPath, remoteFileId);
        var contentPath = !string.IsNullOrWhiteSpace(resolvedArchivePath)
            ? resolvedArchivePath
            : Path.GetDirectoryName(descriptorPath) ?? string.Empty;
        var resolvedContentPath = ResolveContentPath(descriptorPath, contentPath, GameUserDirectory);
        var coverImagePath = ResolveCoverImagePath(descriptorPath, resolvedContentPath, picture);
        var coverImage = TryLoadCoverImage(resolvedContentPath, picture);

        return new ModEntry(
            id,
            title,
            descriptorPath,
            resolvedArchivePath ?? string.Empty,
            remoteFileId ?? string.Empty,
            launcherPath,
            resolvedContentPath,
            version ?? string.Empty,
            coverImagePath,
            coverImage);
    }

    private static DlcEntry ParseDlcDescriptor(string gameDirectory, string descriptorPath)
    {
        var values = ReadClausewitzKeyValues(descriptorPath);
        if (!values.TryGetValue("name", out var title))
        {
            title = Path.GetFileNameWithoutExtension(descriptorPath);
        }

        var launcherPath = Path.GetRelativePath(gameDirectory, descriptorPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return new DlcEntry(launcherPath, title, descriptorPath, launcherPath);
    }

    private string GetLauncherModPath(string descriptorPath, string? remoteFileId)
    {
        if (!string.IsNullOrWhiteSpace(remoteFileId))
        {
            return $"mod/ugc_{remoteFileId}.mod";
        }

        var localModDirectory = Path.Combine(GameUserDirectory, "mod");
        if (TryGetLocalModLauncherPath(descriptorPath, localModDirectory, out var localLauncherPath))
        {
            return localLauncherPath;
        }

        return $"mod/{GetGeneratedDescriptorFileName(descriptorPath, remoteFileId)}";
    }

    private string EnsureLauncherModDescriptor(ModEntry mod)
    {
        var localModDirectory = Path.Combine(GameUserDirectory, "mod");

        if (TryGetLocalModLauncherPath(mod.DescriptorPath, localModDirectory, out var localLauncherPath))
        {
            return localLauncherPath;
        }

        Directory.CreateDirectory(localModDirectory);
        var descriptorPath = Path.Combine(localModDirectory,
            GetGeneratedDescriptorFileName(mod.DescriptorPath, mod.RemoteFileId));
        var contentPath = ResolveModContentPath(mod);
        var contentKey = File.Exists(contentPath) ? "archive" : "path";
        var lines = new List<string>
        {
            $"name=\"{EscapeClausewitzString(mod.Title)}\"",
            $"{contentKey}=\"{EscapeClausewitzString(contentPath)}\"",
        };

        if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
        {
            lines.Add($"remote_file_id=\"{EscapeClausewitzString(mod.RemoteFileId)}\"");
        }

        File.WriteAllText(
            descriptorPath,
            string.Join(Environment.NewLine, lines.Concat([string.Empty])));

        return $"mod/{Path.GetFileName(descriptorPath)}";
    }

    private static bool TryGetLocalModLauncherPath(string descriptorPath, string localModDirectory,
        out string launcherPath)
    {
        var descriptorFullPath = Path.GetFullPath(descriptorPath);
        var localModFullPath = Path.GetFullPath(localModDirectory);
        var localModPrefix = localModFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? localModFullPath
            : localModFullPath + Path.DirectorySeparatorChar;

        if (descriptorFullPath.StartsWith(localModPrefix, StringComparison.OrdinalIgnoreCase))
        {
            launcherPath = $"mod/{Path.GetFileName(descriptorPath)}";
            return true;
        }

        launcherPath = string.Empty;
        return false;
    }

    private string ResolveModContentPath(ModEntry mod)
    {
        var contentPath = !string.IsNullOrWhiteSpace(mod.ContentPath)
            ? mod.ContentPath
            : Path.GetDirectoryName(mod.DescriptorPath) ?? string.Empty;

        return ResolveContentPath(mod.DescriptorPath, contentPath, GameUserDirectory);
    }

    private static string ResolveContentPath(string descriptorPath, string contentPath, string? gameUserDirectory)
    {
        if (Path.IsPathRooted(contentPath) || string.IsNullOrWhiteSpace(contentPath))
        {
            return contentPath;
        }

        if (!string.IsNullOrWhiteSpace(gameUserDirectory))
        {
            var userDirectoryCandidate = Path.GetFullPath(Path.Combine(gameUserDirectory, contentPath));
            if (File.Exists(userDirectoryCandidate) || Directory.Exists(userDirectoryCandidate))
            {
                return userDirectoryCandidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(descriptorPath))
        {
            var descriptorDirectory = Path.GetDirectoryName(descriptorPath);
            if (!string.IsNullOrWhiteSpace(descriptorDirectory))
            {
                contentPath = Path.GetFullPath(Path.Combine(descriptorDirectory, contentPath));
            }
        }

        return contentPath;
    }

    private static Bitmap? TryLoadCoverImage(string contentPath, string? picture)
    {
        if (string.IsNullOrWhiteSpace(picture)
            || string.IsNullOrWhiteSpace(contentPath)
            || !File.Exists(contentPath)
            || !string.Equals(Path.GetExtension(contentPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var archive = ZipFile.OpenRead(contentPath);
            var entry = archive.Entries.FirstOrDefault(item =>
                string.Equals(item.FullName.Replace('\\', '/'), picture.Replace('\\', '/'),
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(item.FullName), Path.GetFileName(picture),
                    StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return null;
            }

            using var sourceStream = entry.Open();
            using var memoryStream = new MemoryStream();
            sourceStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return new Bitmap(memoryStream);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveCoverImagePath(string descriptorPath, string contentPath, string? picture)
    {
        if (string.IsNullOrWhiteSpace(picture))
        {
            return string.Empty;
        }

        var candidates = new List<string>();
        if (Path.IsPathRooted(picture))
        {
            candidates.Add(picture);
        }
        else
        {
            if (Directory.Exists(contentPath))
            {
                candidates.Add(Path.Combine(contentPath, picture));
            }

            var descriptorDirectory = Path.GetDirectoryName(descriptorPath);
            if (!string.IsNullOrWhiteSpace(descriptorDirectory))
            {
                candidates.Add(Path.Combine(descriptorDirectory, picture));
            }
        }

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static string EscapeClausewitzString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetGeneratedDescriptorFileName(string descriptorPath, string? remoteFileId)
    {
        if (!string.IsNullOrWhiteSpace(remoteFileId))
        {
            return $"ugc_{remoteFileId}.mod";
        }

        var name = Path.GetFileNameWithoutExtension(descriptorPath);
        if (name.Equals("descriptor", StringComparison.OrdinalIgnoreCase))
        {
            name = Path.GetFileName(Path.GetDirectoryName(descriptorPath)) ?? name;
        }

        name = SanitizeFileName(string.IsNullOrWhiteSpace(name) ? "shadow_mod" : name);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(descriptorPath)))
            .ToLowerInvariant()[..8];
        return $"{name}_{hash}.mod";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "shadow_mod" : sanitized;
    }

    private static Dictionary<string, string> ReadClausewitzKeyValues(string filePath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim();
            var value = line[(equalsIndex + 1)..].Trim().Trim('"');
            values[key] = value;
        }

        return values;
    }

    private static IReadOnlyList<(string Id, string Name)> ReadParadoxPlaysets(SqliteConnection connection)
    {
        var playsets = new List<(string Id, string Name)>();
        var columns = GetTableColumns(connection, "playsets");
        var hasIsRemoved = columns.Contains("isRemoved");
        var hasIsActive = columns.Contains("isActive");

        using var command = connection.CreateCommand();
        command.CommandText =
            $"select * from playsets{(hasIsRemoved ? " where coalesce(isRemoved, 0) = 0" : string.Empty)} order by {(hasIsActive ? "coalesce(isActive, 0) desc, " : string.Empty)}name";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = ReadString(reader, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var name = ReadString(reader, "name");
            playsets.Add((id, string.IsNullOrWhiteSpace(name) ? id : name));
        }

        return playsets;
    }

    private static IReadOnlyList<(string PlaysetId, string ModId, int Position, bool Enabled)> ReadParadoxPlaysetMods(
        SqliteConnection connection)
    {
        var playsetMods = new List<(string PlaysetId, string ModId, int Position, bool Enabled)>();
        using var command = connection.CreateCommand();
        command.CommandText = "select playsetId, modId, position, enabled from playsets_mods";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var playsetId = ReadString(reader, "playsetId");
            var modId = ReadString(reader, "modId");
            if (string.IsNullOrWhiteSpace(playsetId) || string.IsNullOrWhiteSpace(modId))
            {
                continue;
            }

            playsetMods.Add((playsetId, modId, ReadPosition(reader, "position"), ReadBoolean(reader, "enabled", true)));
        }

        return playsetMods;
    }

    private static Dictionary<string, string> ReadParadoxMods(SqliteConnection connection)
    {
        var mods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var columns = GetTableColumns(connection, "mods");
        var aliasColumns = new[] { "steamId", "gameRegistryId", "pdxId", "id" }
            .Where(columns.Contains)
            .ToArray();

        using var command = connection.CreateCommand();
        command.CommandText = "select * from mods";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = ReadString(reader, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            foreach (var alias in aliasColumns.Select(column => ReadString(reader, column)))
            {
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    mods[id] = alias;
                    break;
                }
            }
        }

        return mods;
    }

    private static HashSet<string> GetTableColumns(SqliteConnection connection, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = $"pragma table_info({tableName})";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = ReadString(reader, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                columns.Add(name);
            }
        }

        return columns;
    }

    private static Dictionary<string, string> BuildParadoxModMap(IEnumerable<ModEntry> knownMods)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in knownMods)
        {
            map[mod.Id] = mod.Id;
            map[mod.LauncherPath] = mod.Id;
            map[mod.DescriptorPath] = mod.Id;

            if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
            {
                map[mod.RemoteFileId] = mod.Id;
            }
        }

        return map;
    }

    private static string ResolveKnownModId(
        string launcherModId,
        IReadOnlyDictionary<string, string> launcherModMap,
        IReadOnlyDictionary<string, string> knownMods)
    {
        if (knownMods.TryGetValue(launcherModId, out var knownModId))
        {
            return knownModId;
        }

        if (launcherModMap.TryGetValue(launcherModId, out var alias)
            && knownMods.TryGetValue(alias, out knownModId))
        {
            return knownModId;
        }

        return alias ?? launcherModId;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $tableName";
        command.Parameters.AddWithValue("$tableName", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static string ReadString(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }

    private static bool ReadBoolean(SqliteDataReader reader, string columnName, bool fallback)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        return reader.GetValue(ordinal) switch
        {
            bool value => value,
            long value => value != 0,
            int value => value != 0,
            string value => value.Equals("true", StringComparison.OrdinalIgnoreCase)
                            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                            || value == "1",
            _ => fallback,
        };
    }

    private static int ReadPosition(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        var value = reader.GetValue(ordinal);
        if (value is long longValue)
        {
            return (int)longValue;
        }

        if (int.TryParse(Convert.ToString(value), out var intValue))
        {
            return intValue;
        }

        var text = Convert.ToString(value);
        return !string.IsNullOrWhiteSpace(text)
               && int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var hexValue)
            ? hexValue
            : 0;
    }

    private static IEnumerable<string> SplitArguments(string commandLine)
    {
        var current = new List<char>();
        var inQuotes = false;

        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Count > 0)
                {
                    yield return new string(current.ToArray());
                    current.Clear();
                }

                continue;
            }

            current.Add(character);
        }

        if (current.Count > 0)
        {
            yield return new string(current.ToArray());
        }
    }
}
