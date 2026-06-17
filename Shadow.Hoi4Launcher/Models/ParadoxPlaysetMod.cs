namespace Shadow.Hoi4Launcher.Models;

internal sealed class ParadoxPlaysetMod
{
    public ParadoxPlaysetMod(string modId, int position, bool enabled)
    {
        ModId = modId;
        Position = position;
        Enabled = enabled;
    }

    public string ModId { get; }

    public int Position { get; }

    public bool Enabled { get; }
}
