using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Microsoft.Data.Sqlite;
using Shadow.ParadoxGameLauncher.Localization;
using Shadow.ParadoxGameLauncher.Models;

namespace Shadow.ParadoxGameLauncher.Services;

public sealed class ParadoxGameLauncherService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private const int CoverDecodeWidth = 184;

    private static readonly string[] CoverImageFileNames =
    [
        "thumbnail.png",
        "thumbnail.jpg",
        "thumbnail.jpeg",
        "thumbnail.webp",
        "thumbnail.gif",
        "thumbnail.bmp",
        "thumb.png",
        "preview.png",
        "icon.png",
    ];

    private readonly ParadoxGameLauncherConfiguration _configuration;

    public ParadoxGameLauncherService(ParadoxGameLauncherConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GameUserDirectory => _configuration.GameUserDirectory;

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

        if (Directory.Exists(_configuration.WorkshopDirectory))
        {
            descriptors.AddRange(Directory.EnumerateFiles(_configuration.WorkshopDirectory, "*.mod",
                SearchOption.AllDirectories));
            descriptors.AddRange(Directory.EnumerateFiles(_configuration.WorkshopDirectory, "descriptor.mod",
                SearchOption.AllDirectories));
        }

        var mods = descriptors
            .Select(ParadoxModIdentity.NormalizePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(ParseModDescriptor);

        return ParadoxModIdentity.Deduplicate(mods);
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
        var enabledModIds = playset.EnabledModIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enabledMods = modList
            .Where(mod => ParadoxModIdentity.ContainsModReference(enabledModIds, mod))
            .ToArray();

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

    public ModEntry ImportModFromArchive(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            throw new FileNotFoundException(ParadoxGameLauncherStrings.Get("Paradox.Service.ImportArchiveMissing"), archivePath);
        }

        var extension = Path.GetExtension(archivePath);
        if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".7z", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".rar", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(ParadoxGameLauncherStrings.Get("Paradox.Service.ImportArchiveUnsupported"));
        }

        if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(ParadoxGameLauncherStrings.Get("Paradox.Service.ImportArchiveZipOnly"));
        }

        if (string.IsNullOrWhiteSpace(GameUserDirectory))
        {
            throw new InvalidOperationException(ParadoxGameLauncherStrings.Get("Paradox.Service.UserDirectoryMissing"));
        }

        var localModDirectory = Path.Combine(GameUserDirectory, "mod");
        Directory.CreateDirectory(localModDirectory);

        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0)
        {
            throw new InvalidOperationException(ParadoxGameLauncherStrings.Get("Paradox.Service.ImportArchiveEmpty"));
        }

        var descriptorEntry = FindImportDescriptorEntry(archive);
        var values = descriptorEntry is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : ReadClausewitzKeyValues(descriptorEntry);

        if (!values.TryGetValue("name", out var title) || string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(archivePath);
        }

        values.TryGetValue("remote_file_id", out var remoteFileId);
        values.TryGetValue("version", out var version);
        if (string.IsNullOrWhiteSpace(version))
        {
            values.TryGetValue("supported_version", out version);
        }

        var folderName = BuildImportedModFolderName(title, remoteFileId, archivePath);
        var contentDirectory = Path.Combine(localModDirectory, folderName);
        if (Directory.Exists(contentDirectory))
        {
            folderName = $"{folderName}_{DateTime.Now:yyyyMMddHHmmss}";
            contentDirectory = Path.Combine(localModDirectory, folderName);
        }

        Directory.CreateDirectory(contentDirectory);
        ExtractZipArchive(archive, contentDirectory, stripRoot: ShouldStripZipRoot(archive, descriptorEntry));

        var extractedDescriptorPath = FindExtractedDescriptorPath(contentDirectory);
        if (!string.IsNullOrWhiteSpace(extractedDescriptorPath)
            && File.Exists(extractedDescriptorPath)
            && string.Equals(Path.GetDirectoryName(extractedDescriptorPath), contentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // Keep extracted descriptor.mod inside the content folder; create a launcher path descriptor next to it.
        }

        var launcherDescriptorName = !string.IsNullOrWhiteSpace(remoteFileId)
            ? $"ugc_{remoteFileId.Trim()}.mod"
            : $"{SanitizeFileName(folderName)}.mod";
        var launcherDescriptorPath = Path.Combine(localModDirectory, launcherDescriptorName);
        if (File.Exists(launcherDescriptorPath)
            && !string.Equals(Path.GetFullPath(launcherDescriptorPath), Path.GetFullPath(extractedDescriptorPath ?? string.Empty),
                StringComparison.OrdinalIgnoreCase))
        {
            launcherDescriptorName = $"{Path.GetFileNameWithoutExtension(launcherDescriptorName)}_{DateTime.Now:yyyyMMddHHmmss}.mod";
            launcherDescriptorPath = Path.Combine(localModDirectory, launcherDescriptorName);
        }

        WriteImportedPathDescriptor(
            launcherDescriptorPath,
            title,
            contentDirectory,
            remoteFileId,
            version,
            values);

        return ParseModDescriptor(launcherDescriptorPath);
    }

    public Process StartGame(IEnumerable<string>? extraArguments = null)
    {
        if (!File.Exists(_configuration.GameExecutablePath))
        {
            throw new InvalidOperationException(ParadoxGameLauncherStrings.Get("Paradox.Service.SelectExecutable"));
        }

        var executablePath = _configuration.GameExecutablePath;
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
        };

        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(_configuration.LaunchArguments))
        {
            arguments.AddRange(SplitArguments(_configuration.LaunchArguments));
        }

        if (extraArguments is not null)
        {
            arguments.AddRange(extraArguments.Where(argument => !string.IsNullOrWhiteSpace(argument)));
        }

        foreach (var argument in arguments.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo)
               ?? throw new InvalidOperationException(ParadoxGameLauncherStrings.Get("Paradox.Service.ProcessFailed"));
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
            throw new InvalidOperationException(ParadoxGameLauncherStrings.Get("Paradox.Service.ModLocationMissing"));
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
        if (File.Exists(_configuration.GameExecutablePath))
        {
            return Path.GetDirectoryName(_configuration.GameExecutablePath);
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

        var resolvedArchivePath = !string.IsNullOrWhiteSpace(archivePath) ? archivePath : archive;
        var normalizedDescriptorPath = ParadoxModIdentity.NormalizePath(descriptorPath);
        var launcherPath = ParadoxModIdentity.NormalizeLauncherPath(
            GetLauncherModPath(normalizedDescriptorPath, remoteFileId));
        var contentPath = !string.IsNullOrWhiteSpace(resolvedArchivePath)
            ? resolvedArchivePath
            : Path.GetDirectoryName(normalizedDescriptorPath) ?? string.Empty;
        var resolvedContentPath = ParadoxModIdentity.NormalizePath(
            ResolveContentPath(normalizedDescriptorPath, contentPath, GameUserDirectory));
        var coverImagePath = ResolveCoverImagePath(normalizedDescriptorPath, resolvedContentPath);
        var coverImage = TryLoadCoverImage(coverImagePath, resolvedContentPath);

        // Build a temporary entry first so non-Steam ids can key off normalized content/descriptor paths.
        var provisional = new ModEntry(
            !string.IsNullOrWhiteSpace(remoteFileId) ? remoteFileId.Trim() : "pending",
            title,
            normalizedDescriptorPath,
            resolvedArchivePath ?? string.Empty,
            remoteFileId?.Trim() ?? string.Empty,
            launcherPath,
            resolvedContentPath,
            version ?? string.Empty,
            coverImagePath,
            coverImage);

        var id = !string.IsNullOrWhiteSpace(remoteFileId)
            ? remoteFileId.Trim()
            : ParadoxModIdentity.GetStableId(provisional);

        return new ModEntry(
            id,
            title,
            normalizedDescriptorPath,
            resolvedArchivePath ?? string.Empty,
            remoteFileId?.Trim() ?? string.Empty,
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
        if (string.IsNullOrWhiteSpace(contentPath))
        {
            return string.Empty;
        }

        // Normalize mixed / and \ before any existence checks or path combining.
        contentPath = contentPath
            .Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(contentPath))
        {
            try
            {
                return Path.GetFullPath(contentPath);
            }
            catch
            {
                return contentPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(gameUserDirectory))
        {
            try
            {
                var userDirectoryCandidate = Path.GetFullPath(Path.Combine(gameUserDirectory, contentPath));
                if (File.Exists(userDirectoryCandidate) || Directory.Exists(userDirectoryCandidate))
                {
                    return userDirectoryCandidate;
                }
            }
            catch
            {
                // Continue with descriptor-relative resolution.
            }
        }

        if (!string.IsNullOrWhiteSpace(descriptorPath))
        {
            var descriptorDirectory = Path.GetDirectoryName(descriptorPath);
            if (!string.IsNullOrWhiteSpace(descriptorDirectory))
            {
                try
                {
                    return Path.GetFullPath(Path.Combine(descriptorDirectory, contentPath));
                }
                catch
                {
                    return Path.Combine(descriptorDirectory, contentPath);
                }
            }
        }

        return contentPath;
    }

    private static Bitmap? TryLoadCoverImage(string coverImagePath, string contentPath)
    {
        if (!string.IsNullOrWhiteSpace(coverImagePath) && File.Exists(coverImagePath))
        {
            try
            {
                using var stream = File.OpenRead(coverImagePath);
                return Bitmap.DecodeToWidth(stream, CoverDecodeWidth);
            }
            catch
            {
                // Fall through to zip content lookup when the resolved path is unreadable.
            }
        }

        if (string.IsNullOrWhiteSpace(contentPath)
            || !File.Exists(contentPath)
            || !string.Equals(Path.GetExtension(contentPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var archive = ZipFile.OpenRead(contentPath);
            var entry = archive.Entries.FirstOrDefault(item =>
            {
                var fileName = Path.GetFileName(item.FullName);
                return CoverImageFileNames.Any(name =>
                    string.Equals(fileName, name, StringComparison.OrdinalIgnoreCase));
            });

            if (entry is null)
            {
                return null;
            }

            using var sourceStream = entry.Open();
            using var memoryStream = new MemoryStream();
            sourceStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return Bitmap.DecodeToWidth(memoryStream, CoverDecodeWidth);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveCoverImagePath(string descriptorPath, string contentPath)
    {
        foreach (var directory in EnumerateCoverImageDirectories(descriptorPath, contentPath))
        {
            foreach (var fileName in CoverImageFileNames)
            {
                var candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateCoverImageDirectories(string descriptorPath, string contentPath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                path = Path.GetFullPath(path);
            }
            catch
            {
                // Keep the original path when GetFullPath fails.
            }

            if (seen.Add(path))
            {
                candidates.Add(path);
            }
        }

        Add(contentPath);

        var descriptorDirectory = Path.GetDirectoryName(descriptorPath);
        Add(descriptorDirectory);

        if (!string.IsNullOrWhiteSpace(contentPath))
        {
            Add(Path.Combine(contentPath, "src"));
        }

        if (!string.IsNullOrWhiteSpace(descriptorDirectory))
        {
            Add(Path.Combine(descriptorDirectory, "src"));
        }

        return candidates;
    }

    private static ZipArchiveEntry? FindImportDescriptorEntry(ZipArchive archive)
    {
        var entries = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .ToArray();

        return entries.FirstOrDefault(entry =>
                   string.Equals(entry.Name, "descriptor.mod", StringComparison.OrdinalIgnoreCase))
               ?? entries.FirstOrDefault(entry =>
                   string.Equals(Path.GetExtension(entry.Name), ".mod", StringComparison.OrdinalIgnoreCase)
                   && entry.FullName.Count(character => character is '/' or '\\') <= 1);
    }

    private static Dictionary<string, string> ReadClausewitzKeyValues(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.ReadLine() is { } rawLine)
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

    private static bool ShouldStripZipRoot(ZipArchive archive, ZipArchiveEntry? descriptorEntry)
    {
        var topLevelNames = archive.Entries
            .Select(GetZipTopLevelName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (topLevelNames.Length != 1)
        {
            return false;
        }

        var rootName = topLevelNames[0];
        var hasRootDirectory = archive.Entries.Any(entry =>
            entry.FullName.Replace('\\', '/').StartsWith(rootName + "/", StringComparison.OrdinalIgnoreCase));

        if (!hasRootDirectory)
        {
            return false;
        }

        if (descriptorEntry is null)
        {
            return true;
        }

        var descriptorPath = descriptorEntry.FullName.Replace('\\', '/');
        return descriptorPath.StartsWith(rootName + "/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(Path.GetFileName(descriptorPath), descriptorPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetZipTopLevelName(ZipArchiveEntry entry)
    {
        var fullName = entry.FullName.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var separatorIndex = fullName.IndexOf('/');
        return separatorIndex < 0 ? fullName : fullName[..separatorIndex];
    }

    private static void ExtractZipArchive(ZipArchive archive, string destinationDirectory, bool stripRoot)
    {
        foreach (var entry in archive.Entries)
        {
            var relativePath = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            if (stripRoot)
            {
                var separatorIndex = relativePath.IndexOf('/');
                if (separatorIndex < 0)
                {
                    // Single top-level file should still extract.
                    if (string.IsNullOrWhiteSpace(entry.Name))
                    {
                        continue;
                    }
                }
                else
                {
                    relativePath = relativePath[(separatorIndex + 1)..];
                }
            }

            relativePath = relativePath.Trim('/');
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, relativePath));
            var destinationRoot = Path.GetFullPath(destinationDirectory);
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Name) || relativePath.EndsWith('/'))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static string? FindExtractedDescriptorPath(string contentDirectory)
    {
        var nestedDescriptor = Directory.EnumerateFiles(contentDirectory, "descriptor.mod", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(nestedDescriptor))
        {
            return nestedDescriptor;
        }

        return Directory.EnumerateFiles(contentDirectory, "*.mod", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    private static string BuildImportedModFolderName(string title, string? remoteFileId, string archivePath)
    {
        if (!string.IsNullOrWhiteSpace(remoteFileId))
        {
            return SanitizeFileName(remoteFileId.Trim());
        }

        var fromTitle = SanitizeFileName(title);
        if (!string.IsNullOrWhiteSpace(fromTitle) && !string.Equals(fromTitle, "shadow_mod", StringComparison.OrdinalIgnoreCase))
        {
            return fromTitle;
        }

        return SanitizeFileName(Path.GetFileNameWithoutExtension(archivePath));
    }

    private static void WriteImportedPathDescriptor(
        string descriptorPath,
        string title,
        string contentDirectory,
        string? remoteFileId,
        string? version,
        IReadOnlyDictionary<string, string> sourceValues)
    {
        var lines = new List<string>
        {
            $"name=\"{EscapeClausewitzString(title)}\"",
            $"path=\"{EscapeClausewitzString(contentDirectory.Replace('\\', '/'))}\"",
        };

        if (!string.IsNullOrWhiteSpace(version))
        {
            if (sourceValues.ContainsKey("supported_version") && !sourceValues.ContainsKey("version"))
            {
                lines.Add($"supported_version=\"{EscapeClausewitzString(version)}\"");
            }
            else
            {
                lines.Add($"version=\"{EscapeClausewitzString(version)}\"");
            }
        }

        if (!string.IsNullOrWhiteSpace(remoteFileId))
        {
            lines.Add($"remote_file_id=\"{EscapeClausewitzString(remoteFileId.Trim())}\"");
        }

        if (sourceValues.TryGetValue("picture", out var picture) && !string.IsNullOrWhiteSpace(picture))
        {
            lines.Add($"picture=\"{EscapeClausewitzString(picture)}\"");
        }

        if (sourceValues.TryGetValue("tags", out var tags) && !string.IsNullOrWhiteSpace(tags))
        {
            // tags are usually multi-line blocks; skip incomplete single-line captures.
        }

        File.WriteAllText(descriptorPath, string.Join(Environment.NewLine, lines.Concat([string.Empty])));
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
            void Add(string? key)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    map.TryAdd(key, mod.Id);
                }
            }

            Add(mod.Id);
            Add(ParadoxModIdentity.GetStableId(mod));
            Add(mod.LauncherPath);
            Add(ParadoxModIdentity.NormalizeLauncherPath(mod.LauncherPath));
            Add(mod.DescriptorPath);
            Add(ParadoxModIdentity.NormalizePath(mod.DescriptorPath));
            Add(mod.ContentPath);
            Add(ParadoxModIdentity.NormalizePath(mod.ContentPath));

            if (!string.IsNullOrWhiteSpace(mod.RemoteFileId))
            {
                Add(mod.RemoteFileId);
                Add($"steam:{mod.RemoteFileId.Trim()}");
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

