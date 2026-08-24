namespace Shadow.ParadoxGameLauncher.Models;

public sealed class ParadoxGameDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string DocumentsFolderName { get; init; }

    /// <summary>
    /// Folder name under %AppData%\Posdaca used for mod index / playset workspace storage.
    /// Defaults to <see cref="DocumentsFolderName"/> when not set.
    /// </summary>
    public string? WorkspaceFolderNameOverride { get; init; }

    public string WorkspaceFolderName =>
        string.IsNullOrWhiteSpace(WorkspaceFolderNameOverride)
            ? DocumentsFolderName
            : WorkspaceFolderNameOverride;

    public required string ExecutableFileName { get; init; }

    public required string SteamAppId { get; init; }

    /// <summary>
    /// Folder name inside the game user directory that holds save files.
    /// </summary>
    public string SaveFolderName { get; init; } = "save games";

    /// <summary>
    /// Save file extensions (lowercase, with dot). An empty entry matches files
    /// without an extension.
    /// </summary>
    public IReadOnlyList<string> SaveFileExtensions { get; init; } = [];

    public IReadOnlyList<string> SteamFolderNames { get; init; } = [];

    public string DefaultUserDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        DocumentsFolderName);

    public override string ToString() => DisplayName;
}
