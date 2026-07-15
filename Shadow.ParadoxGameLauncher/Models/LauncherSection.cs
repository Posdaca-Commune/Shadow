using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;

namespace Shadow.ParadoxGameLauncher.Models;

public sealed class LauncherSection : ObservableObject
{
    private readonly string _title;
    private readonly string _description;

    public LauncherSection(string key, string title, string description, FASymbol symbol)
    {
        Key = key;
        _title = title;
        _description = description;
        Symbol = symbol;
        ShadowLocalizer.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
        };
    }

    public string Key { get; }

    public string Title => LocalizedText.Resolve(_title);

    public string Description => LocalizedText.Resolve(_description);

    public FASymbol Symbol { get; }
}
