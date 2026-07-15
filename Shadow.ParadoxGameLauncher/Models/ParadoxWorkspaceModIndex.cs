namespace Shadow.ParadoxGameLauncher.Models;

public sealed record ParadoxWorkspaceModIndex(
    string SchemaVersion,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ParadoxWorkspaceModIndexEntry> Mods);