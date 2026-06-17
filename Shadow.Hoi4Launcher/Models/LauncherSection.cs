namespace Shadow.Hoi4Launcher.Models;

using FluentAvalonia.UI.Controls;

public sealed class LauncherSection
{
    public LauncherSection(string key, string title, FASymbol symbol)
    {
        Key = key;
        Title = title;
        Symbol = symbol;
    }

    public string Key { get; }

    public string Title { get; }

    public FASymbol Symbol { get; }
}
