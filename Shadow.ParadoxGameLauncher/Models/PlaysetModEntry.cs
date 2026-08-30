using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;

namespace Shadow.ParadoxGameLauncher.Models;

public sealed partial class PlaysetModEntry : ObservableObject
{
    public PlaysetModEntry(ModEntry mod, bool isEnabled)
    {
        Mod = mod;
        IsEnabled = isEnabled;
    }

    public ModEntry Mod { get; }

    public string Id => Mod.Id;

    public string Title => Mod.Title;

    public string Subtitle => Mod.Subtitle;

    public string RemoteFileId => Mod.RemoteFileId;

    public string VersionLabel => Mod.VersionLabel;

    public string SourceLabel => Mod.SourceLabel;

    public Bitmap? CoverImage => Mod.CoverImage;

    public bool HasCoverImage => Mod.HasCoverImage;

    public bool IsCoverPlaceholderVisible => Mod.IsCoverPlaceholderVisible;

    public bool CanOpenWorkshopPage => Mod.CanOpenWorkshopPage;

    // Raised on the wrapper so bindings that target PlaysetModEntry itself refresh
    // when the owning view model reports a culture change (see ModEntry.RaiseStringsChanged).
    public void RaiseStringsChanged()
    {
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(VersionLabel));
    }

    [ObservableProperty]
    private bool _isEnabled;
}
