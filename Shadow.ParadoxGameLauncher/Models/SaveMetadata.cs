namespace Shadow.ParadoxGameLauncher.Models;

public sealed class SaveMetadata
{
    /// <summary>
    /// In-game date, e.g. "1936.1.1" or "2200.01.09".
    /// Null when not available.
    /// </summary>
    public string? Date { get; init; }

    /// <summary>
    /// Player country tag, e.g. "GER", "ENG", "PRC".
    /// Null when not available.
    /// </summary>
    public string? PlayerCountry { get; init; }

    /// <summary>
    /// Game version string from the save header.
    /// </summary>
    public string? GameVersion { get; init; }

    /// <summary>
    /// Save name set by the player.
    /// </summary>
    public string? SaveName { get; init; }

    /// <summary>
    /// True when at least one metadata field was extracted.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Date)
        || !string.IsNullOrWhiteSpace(PlayerCountry)
        || !string.IsNullOrWhiteSpace(SaveName);
}
