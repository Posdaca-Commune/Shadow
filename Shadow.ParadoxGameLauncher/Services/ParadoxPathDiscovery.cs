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

        var direct = Path.Combine(directory, game.ExecutableFileName);
        if (File.Exists(direct))
        {
            return direct;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, game.ExecutableFileName, SearchOption.AllDirectories))
            {
                return file;
            }
        }
        catch
        {
            // Best-effort only: install folders can contain inaccessible subdirectories.
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
        yield return game.DefaultUserDirectory;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, "OneDrive", "Documents", "Paradox Interactive", game.DocumentsFolderName);
            yield return Path.Combine(userProfile, "Documents", "Paradox Interactive", game.DocumentsFolderName);
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
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");

        if (OperatingSystem.IsWindows())
        {
            foreach (var registryRoot in ReadSteamRootsFromRegistry())
            {
                yield return registryRoot;
            }
        }
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

        foreach (var line in lines)
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
            if (Directory.Exists(path))
            {
                yield return Path.GetFullPath(path);
            }
        }
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