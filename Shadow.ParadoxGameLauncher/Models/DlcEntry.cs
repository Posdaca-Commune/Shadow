namespace Shadow.ParadoxGameLauncher.Models;

public sealed class DlcEntry : SelectableItem
{
    public DlcEntry(string id, string title, string path, string launcherPath)
        : base(id, title, string.Empty, true)
    {
        Path = path;
        LauncherPath = launcherPath;
    }

    public string Path { get; }

    public string LauncherPath { get; }
}
