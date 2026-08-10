namespace Shadow.Abstractions;

/// <summary>
/// Optional plugin surface that contributes live status to the host home launch pad.
/// </summary>
public interface IShadowHomeStatusProvider
{
    ShadowHomeStatus GetHomeStatus();
}

/// <summary>
/// Snapshot shown on the host home page for a plugin feature.
/// </summary>
/// <param name="Title">Feature title.</param>
/// <param name="Summary">Primary status line, such as current game or playset.</param>
/// <param name="Detail">Secondary detail line.</param>
/// <param name="NeedsSetup">Whether the feature still needs first-time configuration.</param>
/// <param name="CanLaunch">Whether a direct launch action is currently available.</param>
/// <param name="Launch">Optional direct launch callback.</param>
public sealed record ShadowHomeStatus(
    string Title,
    string Summary,
    string? Detail = null,
    bool NeedsSetup = false,
    bool CanLaunch = false,
    Action? Launch = null);
