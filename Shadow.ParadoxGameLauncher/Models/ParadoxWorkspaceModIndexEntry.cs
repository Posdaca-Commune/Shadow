namespace Shadow.ParadoxGameLauncher.Models;

public sealed record ParadoxWorkspaceModIndexEntry(
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