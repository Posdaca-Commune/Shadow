namespace Shadow.Hoi4Launcher.Models;

public sealed record Hoi4WorkspaceModIndex(
    string SchemaVersion,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Hoi4WorkspaceModIndexEntry> Mods);