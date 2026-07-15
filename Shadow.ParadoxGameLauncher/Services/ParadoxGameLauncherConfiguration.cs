using System.Text.Json;
using Shadow.ParadoxGameLauncher.Models;

namespace Shadow.ParadoxGameLauncher.Services;

public sealed class ParadoxGameLauncherConfiguration
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _pluginDataDirectory;
    private ParadoxWorkspacePlaysetStore _playsetStore;

    private ParadoxGameLauncherConfiguration(
        string pluginDataDirectory,
        string statePath,
        ParadoxGameLauncherState state)
    {
        _pluginDataDirectory = pluginDataDirectory;
        StatePath = statePath;
        State = state;
        MigrateLegacyStateIfNeeded();
        EnsureSelectedGame();
        _playsetStore = CreatePlaysetStore(SelectedGame.Id);
        ApplyActiveGameDefaults();
    }

    public string StatePath { get; }

    public ParadoxGameLauncherState State { get; }

    public ParadoxWorkspacePlaysetStore PlaysetStore => _playsetStore;

    public ParadoxGameDefinition SelectedGame => ParadoxGameCatalog.GetById(State.SelectedGameId);

    public ParadoxGameProfileState ActiveProfile => State.GetOrCreateProfile(SelectedGame.Id);

    public string GameExecutablePath
    {
        get => ActiveProfile.GameExecutablePath;
        set => ActiveProfile.GameExecutablePath = value;
    }

    public string GameUserDirectory
    {
        get => ActiveProfile.GameUserDirectory;
        set => ActiveProfile.GameUserDirectory = value;
    }

    public string WorkshopDirectory
    {
        get => ActiveProfile.WorkshopDirectory;
        set => ActiveProfile.WorkshopDirectory = value;
    }

    public string LaunchArguments
    {
        get => ActiveProfile.LaunchArguments;
        set => ActiveProfile.LaunchArguments = value;
    }

    public bool CloseAfterLaunch
    {
        get => ActiveProfile.CloseAfterLaunch;
        set => ActiveProfile.CloseAfterLaunch = value;
    }

    public string SelectedPlaysetId
    {
        get => ActiveProfile.SelectedPlaysetId;
        set => ActiveProfile.SelectedPlaysetId = value;
    }

    public static ParadoxGameLauncherConfiguration Load(string pluginDataDirectory)
    {
        Directory.CreateDirectory(pluginDataDirectory);
        var statePath = Path.Combine(pluginDataDirectory, "launcher-state.json");

        if (!File.Exists(statePath))
        {
            return new ParadoxGameLauncherConfiguration(pluginDataDirectory, statePath, new ParadoxGameLauncherState());
        }

        try
        {
            var state = JsonSerializer.Deserialize<ParadoxGameLauncherState>(File.ReadAllText(statePath), SerializerOptions)
                        ?? new ParadoxGameLauncherState();
            return new ParadoxGameLauncherConfiguration(pluginDataDirectory, statePath, state);
        }
        catch
        {
            return new ParadoxGameLauncherConfiguration(pluginDataDirectory, statePath, new ParadoxGameLauncherState());
        }
    }

    public void Save()
    {
        // Keep top-level legacy mirrors in sync for readability/debugging.
        State.GameExecutablePath = GameExecutablePath;
        State.GameUserDirectory = GameUserDirectory;
        State.WorkshopDirectory = WorkshopDirectory;
        State.LaunchArguments = LaunchArguments;
        State.CloseAfterLaunch = CloseAfterLaunch;
        State.SelectedPlaysetId = SelectedPlaysetId;

        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(State, SerializerOptions));
    }

    public void SelectGame(string gameId)
    {
        var game = ParadoxGameCatalog.GetById(gameId);
        if (string.Equals(State.SelectedGameId, game.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Save();
        State.SelectedGameId = game.Id;
        _playsetStore = CreatePlaysetStore(game.Id);
        ApplyActiveGameDefaults();
        Save();
    }

    private ParadoxWorkspacePlaysetStore CreatePlaysetStore(string gameId)
    {
        var game = ParadoxGameCatalog.GetById(gameId);
        var store = ParadoxWorkspacePlaysetStore.CreateForGame(game);
        TryMigrateLegacyWorkspace(game, store);
        return store;
    }

    private void TryMigrateLegacyWorkspace(ParadoxGameDefinition game, ParadoxWorkspacePlaysetStore targetStore)
    {
        // Prefer the new Posdaca\{GameName} layout. If empty, import from older plugin-local storage.
        if (targetStore.LoadPlaysets().Count > 0 || File.Exists(targetStore.ModIndexPath))
        {
            return;
        }

        var legacyCandidates = new List<string>
        {
            Path.Combine(_pluginDataDirectory, "games", game.Id),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Shadow",
                "ParadoxGameLauncher",
                "workspace"),
        };

        if (string.Equals(game.Id, "hoi4", StringComparison.OrdinalIgnoreCase))
        {
            legacyCandidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Posdaca",
                "Hoi4Workspace"));
        }

        foreach (var legacyDirectory in legacyCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(legacyDirectory)
                || string.Equals(
                    Path.GetFullPath(legacyDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(targetStore.WorkspaceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var legacyStore = new ParadoxWorkspacePlaysetStore(legacyDirectory);
            var importedAny = false;

            foreach (var playset in legacyStore.LoadPlaysets())
            {
                targetStore.SavePlayset(playset);
                importedAny = true;
            }

            if (File.Exists(legacyStore.ModIndexPath))
            {
                Directory.CreateDirectory(targetStore.ModsDirectory);
                File.Copy(legacyStore.ModIndexPath, targetStore.ModIndexPath, overwrite: false);
                importedAny = true;
            }

            if (importedAny)
            {
                return;
            }
        }
    }

    private void EnsureSelectedGame()
    {
        State.SelectedGameId = ParadoxGameCatalog.GetById(State.SelectedGameId).Id;
        foreach (var game in ParadoxGameCatalog.Games)
        {
            State.GetOrCreateProfile(game.Id);
        }
    }

    private void MigrateLegacyStateIfNeeded()
    {
        if (State.Games.Count > 0)
        {
            return;
        }

        var hasLegacyValues =
            !string.IsNullOrWhiteSpace(State.GameExecutablePath)
            || !string.IsNullOrWhiteSpace(State.GameUserDirectory)
            || !string.IsNullOrWhiteSpace(State.WorkshopDirectory)
            || !string.IsNullOrWhiteSpace(State.LaunchArguments)
            || State.CloseAfterLaunch
            || (!string.IsNullOrWhiteSpace(State.SelectedPlaysetId) && State.SelectedPlaysetId != "default")
            || State.Playsets.Count > 0;

        if (!hasLegacyValues)
        {
            return;
        }

        var hoi4 = State.GetOrCreateProfile("hoi4");
        hoi4.GameExecutablePath = State.GameExecutablePath;
        hoi4.GameUserDirectory = State.GameUserDirectory;
        hoi4.WorkshopDirectory = State.WorkshopDirectory;
        hoi4.LaunchArguments = State.LaunchArguments;
        hoi4.CloseAfterLaunch = State.CloseAfterLaunch;
        hoi4.SelectedPlaysetId = string.IsNullOrWhiteSpace(State.SelectedPlaysetId)
            ? "default"
            : State.SelectedPlaysetId;

        // Seed HOI4 playsets from the previous shared store if the new store is empty.
        var legacyStore = new ParadoxWorkspacePlaysetStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Posdaca",
            "Hoi4Workspace"));
        var newStore = CreatePlaysetStore("hoi4");
        if (newStore.LoadPlaysets().Count == 0)
        {
            var legacyPlaysets = legacyStore.LoadPlaysets();
            if (legacyPlaysets.Count == 0 && State.Playsets.Count > 0)
            {
                legacyPlaysets = State.Playsets;
            }

            foreach (var playset in legacyPlaysets)
            {
                newStore.SavePlayset(playset);
            }
        }

        State.SelectedGameId = "hoi4";
        State.Playsets.Clear();
    }

    private void ApplyActiveGameDefaults()
    {
        var game = SelectedGame;
        var profile = ActiveProfile;

        if (string.IsNullOrWhiteSpace(profile.GameUserDirectory))
        {
            profile.GameUserDirectory = game.DefaultUserDirectory;
        }

        if (string.IsNullOrWhiteSpace(profile.WorkshopDirectory))
        {
            profile.WorkshopDirectory = TryDiscoverWorkshopDirectory(game) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(profile.GameExecutablePath))
        {
            profile.GameExecutablePath = TryDiscoverGameExecutable(game) ?? string.Empty;
        }

        foreach (var playset in State.Playsets.Where(playset =>
                     playset.ModIds.Count == 0 && playset.EnabledModIds.Count > 0))
        {
            playset.ModIds = playset.EnabledModIds.ToList();
        }

        var sharedPlaysets = PlaysetStore.LoadPlaysets();
        if (sharedPlaysets.Count == 0)
        {
            var defaultPlayset = Playset.CreateDefault();
            PlaysetStore.SavePlayset(defaultPlayset);
            sharedPlaysets = [defaultPlayset];
        }

        if (State.Playsets.Count > 0)
        {
            State.Playsets.Clear();
        }

        if (string.IsNullOrWhiteSpace(profile.SelectedPlaysetId)
            || sharedPlaysets.All(playset => playset.Id != profile.SelectedPlaysetId))
        {
            profile.SelectedPlaysetId = sharedPlaysets.FirstOrDefault()?.Id ?? Playset.CreateDefault().Id;
        }
    }

    private static string? TryDiscoverGameExecutable(ParadoxGameDefinition game)
    {
        foreach (var candidate in EnumerateSteamLibraryRoots())
        {
            foreach (var folderName in game.SteamFolderNames)
            {
                var executablePath = Path.Combine(candidate, "steamapps", "common", folderName.TrimEnd('/', '\\'), game.ExecutableFileName);
                if (File.Exists(executablePath))
                {
                    return executablePath;
                }
            }
        }

        return null;
    }

    private static string? TryDiscoverWorkshopDirectory(ParadoxGameDefinition game)
    {
        foreach (var candidate in EnumerateSteamLibraryRoots())
        {
            var workshopPath = Path.Combine(candidate, "steamapps", "workshop", "content", game.SteamAppId);
            if (Directory.Exists(workshopPath))
            {
                return workshopPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSteamLibraryRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var defaultRoot in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
                 })
        {
            if (Directory.Exists(defaultRoot))
            {
                roots.Add(defaultRoot);
            }
        }

        foreach (var root in roots.ToArray())
        {
            var libraryFoldersPath = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersPath))
            {
                continue;
            }

            try
            {
                foreach (var line in File.ReadLines(libraryFoldersPath))
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
                        roots.Add(path);
                    }
                }
            }
            catch
            {
                // Steam library discovery is best-effort only.
            }
        }

        return roots;
    }
}
