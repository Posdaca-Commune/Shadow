namespace Shadow.Hoi4Launcher.Models;

public sealed record Hoi4WorkspaceModIndexEntry(
    string Id,
    string ShadowId,
    string Name,
    string Source,
    string RemoteFileId,
    string DescriptorPath,
    string LauncherPath,
    string ContentPath,
    string Version,
    IReadOnlyList<string> IdentityKeys);