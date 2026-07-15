namespace Shadow.ParadoxGameLauncher.Models;

public sealed class ParadoxGameProfileState
{
    public string GameExecutablePath { get; set; } = string.Empty;

    public string GameUserDirectory { get; set; } = string.Empty;

    public string WorkshopDirectory { get; set; } = string.Empty;

    public string LaunchArguments { get; set; } = string.Empty;

    public bool CloseAfterLaunch { get; set; }

    public string SelectedPlaysetId { get; set; } = "default";
}
