namespace Shadow.Hoi4Launcher.Models;

public sealed record Hoi4WorkspaceModIndexEntry(
    string Id,
    string Name,
    string Source,
    string RemoteFileId,
    string DescriptorPath,
    string LauncherPath,
    string ContentPath,
    string Version);