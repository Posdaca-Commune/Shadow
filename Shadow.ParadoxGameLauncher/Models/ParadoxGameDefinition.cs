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

    /// <summary>
    /// Executable paths relative to the Steam install directory used on
    /// Linux/macOS builds of the game (e.g. "binaries/ck3"). The game's
    /// <see cref="Id"/> is always tried as a last-resort candidate.
    /// </summary>
    public IReadOnlyList<string> LinuxExecutableFileNames { get; init; } = [];

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

    /// <summary>
    /// Executable file names to probe for the given platform, most likely first.
    /// Native Windows builds use "<id>.exe"; Linux/macOS builds ship differently
    /// named binaries per game.
    /// </summary>
    public IReadOnlyList<string> GetExecutableFileNames(bool isWindows)
    {
        if (isWindows)
        {
            return [ExecutableFileName];
        }

        var candidates = new List<string>(LinuxExecutableFileNames) { Id };
        return candidates
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string DefaultUserDirectory => OperatingSystem.IsWindows()
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive",
            DocumentsFolderName)
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Paradox Interactive",
            DocumentsFolderName);

    public override string ToString() => DisplayName;
}
