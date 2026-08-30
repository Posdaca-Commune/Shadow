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
        Save();
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
            // Keep the unreadable file around for manual recovery instead of
            // letting the constructor's initial Save() wipe it silently.
            TryBackupCorruptState(statePath);
            return new ParadoxGameLauncherConfiguration(pluginDataDirectory, statePath, new ParadoxGameLauncherState());
        }
    }

    private static void TryBackupCorruptState(string statePath)
    {
        try
        {
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            File.Copy(statePath, $"{statePath}.corrupt-{stamp}.bak", overwrite: true);
        }
        catch
        {
            // Best effort only; never block startup over a failed backup.
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
        // Write to a temp file and replace, mirroring ParadoxWorkspacePlaysetStore,
        // so a crash mid-write cannot leave a torn launcher-state.json behind.
        var tempPath = StatePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(State, SerializerOptions));
        File.Move(tempPath, StatePath, overwrite: true);
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

        if (!ParadoxPathDiscovery.IsExistingDirectory(profile.GameUserDirectory))
        {
            profile.GameUserDirectory = ParadoxPathDiscovery.TryDiscoverUserDirectory(game) ?? game.DefaultUserDirectory;
        }

        if (!ParadoxPathDiscovery.IsExistingFile(profile.GameExecutablePath))
        {
            profile.GameExecutablePath = ParadoxPathDiscovery.TryDiscoverGameExecutable(game) ?? profile.GameExecutablePath ?? string.Empty;
        }

        if (!ParadoxPathDiscovery.IsExistingDirectory(profile.WorkshopDirectory))
        {
            profile.WorkshopDirectory = ParadoxPathDiscovery.TryDiscoverWorkshopDirectory(game, profile.GameExecutablePath) ?? profile.WorkshopDirectory ?? string.Empty;
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

}

