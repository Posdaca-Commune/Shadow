namespace Shadow.ParadoxGameLauncher.Models;

public sealed class ParadoxGameLauncherState
{
    public string SelectedGameId { get; set; } = "hoi4";

    public Dictionary<string, ParadoxGameProfileState> Games { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Legacy single-game fields kept for migration from the HOI4 launcher state format.
    public string GameExecutablePath { get; set; } = string.Empty;

    public string GameUserDirectory { get; set; } = string.Empty;

    public string WorkshopDirectory { get; set; } = string.Empty;

    public string LaunchArguments { get; set; } = string.Empty;

    public bool CloseAfterLaunch { get; set; }

    public string SelectedPlaysetId { get; set; } = "default";

    public List<Playset> Playsets { get; set; } = [];

    public ParadoxGameProfileState GetOrCreateProfile(string gameId)
    {
        if (!Games.TryGetValue(gameId, out var profile))
        {
            profile = new ParadoxGameProfileState();
            Games[gameId] = profile;
        }

        return profile;
    }
}
