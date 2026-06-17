using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;

namespace Shadow.Hoi4Launcher.Models;

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

    [ObservableProperty]
    private bool _isEnabled;
}
