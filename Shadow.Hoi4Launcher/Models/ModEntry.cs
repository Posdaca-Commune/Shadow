using Avalonia.Media.Imaging;

namespace Shadow.Hoi4Launcher.Models;

public sealed partial class ModEntry : SelectableItem
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

    public string SourceLabel => IsSteamWorkshopMod ? "Steam 创意工坊" : "本地 Mod";

    public string VersionLabel => string.IsNullOrWhiteSpace(Version) ? "版本未知" : Version;

    public string WorkshopUrl => IsSteamWorkshopMod
        ? $"https://steamcommunity.com/sharedfiles/filedetails/?id={RemoteFileId}"
        : string.Empty;

    private static Bitmap? TryLoadCoverImage(string coverImagePath)
    {
        if (string.IsNullOrWhiteSpace(coverImagePath) || !File.Exists(coverImagePath))
        {
            return null;
        }

        try
        {
            return new Bitmap(coverImagePath);
        }
        catch
        {
            return null;
        }
    }
}
