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

    public IReadOnlyList<string> SteamFolderNames { get; init; } = [];

    public string DefaultUserDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        DocumentsFolderName);

    public override string ToString() => DisplayName;
}
