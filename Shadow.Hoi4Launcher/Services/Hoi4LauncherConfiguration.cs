using System.Text.Json;
using Shadow.Hoi4Launcher.Models;

namespace Shadow.Hoi4Launcher.Services;

public sealed class Hoi4LauncherConfiguration
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private Hoi4LauncherConfiguration(string statePath, Hoi4LauncherState state, Hoi4WorkspacePlaysetStore playsetStore)
    {
        StatePath = statePath;
        State = state;
        PlaysetStore = playsetStore;
        ApplyDefaults();
    }

    public string StatePath { get; }

    public Hoi4LauncherState State { get; }

    public Hoi4WorkspacePlaysetStore PlaysetStore { get; }

    public static Hoi4LauncherConfiguration Load(string pluginDataDirectory)
    {
        Directory.CreateDirectory(pluginDataDirectory);
        var statePath = Path.Combine(pluginDataDirectory, "launcher-state.json");
        var playsetStore = Hoi4WorkspacePlaysetStore.CreateDefault();

        if (!File.Exists(statePath))
        {
            return new Hoi4LauncherConfiguration(statePath, new Hoi4LauncherState(), playsetStore);
        }

        try
        {
            var state = JsonSerializer.Deserialize<Hoi4LauncherState>(File.ReadAllText(statePath), SerializerOptions)
                        ?? new Hoi4LauncherState();
            return new Hoi4LauncherConfiguration(statePath, state, playsetStore);
        }
        catch
        {
            return new Hoi4LauncherConfiguration(statePath, new Hoi4LauncherState(), playsetStore);
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(State, SerializerOptions));
    }

    private void ApplyDefaults()
    {
        if (string.IsNullOrWhiteSpace(State.GameUserDirectory))
        {
            State.GameUserDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Paradox Interactive",
                "Hearts of Iron IV");
        }

        foreach (var playset in State.Playsets.Where(playset =>
                     playset.ModIds.Count == 0 && playset.EnabledModIds.Count > 0))
        {
            playset.ModIds = playset.EnabledModIds.ToList();
        }

        var sharedPlaysets = PlaysetStore.LoadPlaysets();
        if (sharedPlaysets.Count == 0)
        {
            var playsetsToSeed = State.Playsets.Count > 0
                ? State.Playsets
                : [Playset.CreateDefault()];

            foreach (var playset in playsetsToSeed)
            {
                PlaysetStore.SavePlayset(playset);
            }

            sharedPlaysets = PlaysetStore.LoadPlaysets();
        }

        if (State.Playsets.Count > 0)
        {
            State.Playsets.Clear();
        }

        if (string.IsNullOrWhiteSpace(State.SelectedPlaysetId)
            || sharedPlaysets.All(playset => playset.Id != State.SelectedPlaysetId))
        {
            State.SelectedPlaysetId = sharedPlaysets.FirstOrDefault()?.Id ?? Playset.CreateDefault().Id;
        }
    }
}