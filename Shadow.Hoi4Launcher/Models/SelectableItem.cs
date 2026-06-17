using CommunityToolkit.Mvvm.ComponentModel;

namespace Shadow.Hoi4Launcher.Models;

public abstract partial class SelectableItem : ObservableObject
{
    protected SelectableItem(string id, string title, string subtitle, bool isEnabled)
    {
        Id = id;
        Title = title;
        Subtitle = subtitle;
        IsEnabled = isEnabled;
    }

    public string Id { get; }

    public string Title { get; }

    public string Subtitle { get; }

    [ObservableProperty]
    private bool _isEnabled;
}
