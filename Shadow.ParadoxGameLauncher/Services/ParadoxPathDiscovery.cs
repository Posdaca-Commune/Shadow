using System.Runtime.Versioning;
using Shadow.ParadoxGameLauncher.Models;

namespace Shadow.ParadoxGameLauncher.Services;

public static class ParadoxPathDiscovery
{
    public static string? TryDiscoverGameExecutable(ParadoxGameDefinition game)
    {
        foreach (var libraryRoot in EnumerateSteamLibraryRoots())
        {
            foreach (var folderName in game.SteamFolderNames)
            {
                var installDirectory = Path.Combine(
                    libraryRoot,
                    "steamapps",
                    "common",
                    folderName.TrimEnd('/', '\\'));
                var executablePath = ResolveExecutableFromDirectory(game, installDirectory);
                if (!string.IsNullOrWhiteSpace(executablePath))
                {
                    return executablePath;
                }
            }
        }

        return null;
    }

    public static string? TryDiscoverUserDirectory(ParadoxGameDefinition game)
    {
        foreach (var candidate in EnumerateUserDirectoryCandidates(game))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return game.DefaultUserDirectory;
    }

    public static string? TryDiscoverWorkshopDirectory(ParadoxGameDefinition game, string? executablePath = null)
    {
        var inferred = InferWorkshopFromExecutable(game, executablePath);
        if (!string.IsNullOrWhiteSpace(inferred) && Directory.Exists(inferred))
        {
            return inferred;
        }

        foreach (var libraryRoot in EnumerateSteamLibraryRoots())
        {
            var workshopPath = Path.Combine(libraryRoot, "steamapps", "workshop", "content", game.SteamAppId);
            if (Directory.Exists(workshopPath))
            {
                return workshopPath;
            }
        }

        return null;
    }

    public static string? ResolveExecutableFromDirectory(ParadoxGameDefinition game, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        // Probe the current platform's names first, then the other platform's,
        // so a Windows host can also resolve a Linux install directory and
        // vice versa.
        var isWindows = OperatingSystem.IsWindows();
        var candidates = game.GetExecutableFileNames(isWindows)
            .Concat(game.GetExecutableFileNames(!isWindows))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var executableName in candidates)
        {
            // GetFullPath also normalizes the candidate's '/' separators to the
            // platform separator, keeping returned paths uniformly formatted.
            var direct = Path.GetFullPath(Path.Combine(directory, executableName));
            if (File.Exists(direct))
            {
                return direct;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, executableName, SearchOption.AllDirectories))
                {
                    return Path.GetFullPath(file);
                }
            }
            catch
            {
                // Best-effort only: install folders can contain inaccessible subdirectories.
            }
        }

        return null;
    }

    public static string? InferWorkshopFromExecutable(ParadoxGameDefinition game, string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(executablePath);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (string.Equals(Path.GetFileName(directory), "steamapps", StringComparison.OrdinalIgnoreCase))
            {
                var workshopPath = Path.Combine(directory, "workshop", "content", game.SteamAppId);
                return workshopPath;
            }

            var parent = Path.GetDirectoryName(directory);
            if (string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            directory = parent;
        }

        return null;
    }

    public static bool IsExistingFile(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public static bool IsExistingDirectory(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    private static IEnumerable<string> EnumerateUserDirectoryCandidates(ParadoxGameDefinition game)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return GetUserDirectoryCandidates(game, userProfile, xdgDataHome, OperatingSystem.IsWindows());
    }

    /// <summary>
    /// Pure candidate enumeration so tests can exercise each platform layout
    /// without touching the real user profile.
    /// </summary>
    internal static IEnumerable<string> GetUserDirectoryCandidates(
        ParadoxGameDefinition game,
        string? userProfile,
        string? xdgDataHome,
        bool isWindows)
    {
        yield return game.DefaultUserDirectory;

        if (isWindows)
        {
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                yield return Path.Combine(userProfile, "OneDrive", "Documents", "Paradox Interactive", game.DocumentsFolderName);
                yield return Path.Combine(userProfile, "Documents", "Paradox Interactive", game.DocumentsFolderName);
            }

            yield break;
        }

        // Linux/macOS: Paradox games keep their user directories under
        // $XDG_DATA_HOME/Paradox Interactive (defaults to ~/.local/share).
        var dataHome = !string.IsNullOrWhiteSpace(xdgDataHome)
            ? xdgDataHome
            : Path.Combine(userProfile ?? string.Empty, ".local", "share");
        if (!string.IsNullOrWhiteSpace(dataHome))
        {
            yield return Path.Combine(dataHome, "Paradox Interactive", game.DocumentsFolderName);
        }

        // Flatpak Steam keeps per-app data under ~/.var/app/<app-id>.
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(
                userProfile, ".var", "app", "com.valvesoftware.Steam", "data",
                "Paradox Interactive", game.DocumentsFolderName);
        }
    }

    private static IEnumerable<string> EnumerateSteamLibraryRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateKnownSteamRoots())
        {
            if (Directory.Exists(candidate))
            {
                roots.Add(Path.GetFullPath(candidate));
            }
        }

        foreach (var root in roots.ToArray())
        {
            foreach (var extraRoot in ReadLibraryFolders(root))
            {
                roots.Add(extraRoot);
            }
        }

        return roots;
    }

    private static IEnumerable<string> EnumerateKnownSteamRoots()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");

            foreach (var registryRoot in ReadSteamRootsFromRegistry())
            {
                yield return registryRoot;
            }

            yield break;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(userProfile, "Library", "Application Support", "Steam");
            yield break;
        }

        // Linux: canonical install paths plus the Flatpak data root. ~/.steam/steam
        // and ~/.steam/root are typically symlinks to ~/.local/share/Steam.
        yield return Path.Combine(userProfile, ".steam", "steam");
        yield return Path.Combine(userProfile, ".steam", "root");
        yield return Path.Combine(userProfile, ".local", "share", "Steam");
        yield return Path.Combine(
            userProfile, ".var", "app", "com.valvesoftware.Steam", "data", "Steam");
    }

    private static IEnumerable<string> ReadLibraryFolders(string steamRoot)
    {
        var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            yield break;
        }

        IEnumerable<string> lines;
        try
        {
            lines = File.ReadLines(libraryFoldersPath);
        }
        catch
        {
            yield break;
        }

        foreach (var path in ExtractLibraryPaths(string.Join(Environment.NewLine, lines)))
        {
            if (Directory.Exists(path))
            {
                yield return Path.GetFullPath(path);
            }
        }
    }

    /// <summary>
    /// Extracts the "path" values from a Steam libraryfolders.vdf file.
    /// Pure string handling so it can be unit-tested.
    /// </summary>
    internal static IReadOnlyList<string> ExtractLibraryPaths(string vdfContent)
    {
        var paths = new List<string>();
        foreach (var line in vdfContent.Split(['\r', '\n']))
        {
            var trimmed = line.Trim();
            if (!trimmed.Contains("\"path\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var path = parts[^1].Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> ReadSteamRootsFromRegistry()
    {
        var paths = new List<string>();
        try
        {
            using var currentUser = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            AddRegistryPath(paths, currentUser?.GetValue("SteamPath") as string);
            AddRegistryPath(paths, currentUser?.GetValue("SteamExe") as string);
        }
        catch
        {
        }

        try
        {
            using var localMachine = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            AddRegistryPath(paths, localMachine?.GetValue("InstallPath") as string);
        }
        catch
        {
        }

        return paths;
    }

    private static void AddRegistryPath(ICollection<string> paths, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var path = value.Replace('/', '\\');
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.GetDirectoryName(path) ?? path;
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            paths.Add(path);
        }
    }
}