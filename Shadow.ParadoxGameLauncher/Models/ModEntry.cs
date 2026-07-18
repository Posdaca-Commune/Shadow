using Avalonia.Media.Imaging;
using Shadow.Abstractions;
using Shadow.ParadoxGameLauncher.Localization;

namespace Shadow.ParadoxGameLauncher.Models;

public sealed class ModEntry : SelectableItem
{
    public ModEntry(
        string id,
        string title,
        string descriptorPath,
        string archivePath,
        string remoteFileId,
        string launcherPath,
        string contentPath,
        string version,
        string coverImagePath,
        Bitmap? coverImage = null)
        : base(id, title, descriptorPath, false)
    {
        DescriptorPath = descriptorPath;
        ArchivePath = archivePath;
        RemoteFileId = remoteFileId;
        LauncherPath = launcherPath;
        ContentPath = contentPath;
        Version = version;
        CoverImagePath = coverImagePath;
        CoverImage = coverImage ?? TryLoadCoverImage(coverImagePath);
        ShadowLocalizer.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SourceLabel));
            OnPropertyChanged(nameof(VersionLabel));
        };
    }

    public string DescriptorPath { get; }

    public string ArchivePath { get; }

    public string RemoteFileId { get; }

    public string LauncherPath { get; }

    public string ContentPath { get; }

    public string Version { get; }

    public string CoverImagePath { get; }

    public Bitmap? CoverImage { get; }

    public bool HasCoverImage => CoverImage is not null;

    public bool IsCoverPlaceholderVisible => !HasCoverImage;

    public bool IsSteamWorkshopMod => !string.IsNullOrWhiteSpace(RemoteFileId);

    public bool CanOpenWorkshopPage => IsSteamWorkshopMod;

    public string SourceLabel => IsSteamWorkshopMod
        ? ParadoxGameLauncherStrings.Get("Paradox.Mod.Source.SteamWorkshop")
        : ParadoxGameLauncherStrings.Get("Paradox.Mod.Source.Local");

    public string VersionLabel => string.IsNullOrWhiteSpace(Version)
        ? ParadoxGameLauncherStrings.Get("Paradox.Mod.VersionUnknown")
        : Version;

    public string WorkshopUrl => IsSteamWorkshopMod
        ? $"https://steamcommunity.com/sharedfiles/filedetails/?id={RemoteFileId}"
        : string.Empty;

    private const int CoverDecodeWidth = 184;

    private static Bitmap? TryLoadCoverImage(string coverImagePath)
    {
        if (string.IsNullOrWhiteSpace(coverImagePath) || !File.Exists(coverImagePath))
        {
            return null;
        }

        try
        {
            // Decode a small thumbnail for list cards instead of full-resolution workshop art.
            return Bitmap.DecodeToWidth(File.OpenRead(coverImagePath), CoverDecodeWidth);
        }
        catch
        {
            return null;
        }
    }
}
