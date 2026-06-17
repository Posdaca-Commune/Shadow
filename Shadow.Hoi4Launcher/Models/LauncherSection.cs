namespace Shadow.Hoi4Launcher.Models;

using FluentAvalonia.UI.Controls;

public sealed class LauncherSection
{
    public LauncherSection(string key, string title, string description, FASymbol symbol)
    {
        Key = key;
        Title = title;
        Description = description;
        Symbol = symbol;
    }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public FASymbol Symbol { get; }
}
