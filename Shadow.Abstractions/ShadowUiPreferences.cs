namespace Shadow.Abstractions;

/// <summary>
/// Host-updated UI preference flags that plugins can read without referencing the host assembly.
/// </summary>
public static class ShadowUiPreferences
{
    public static bool EnableAnimations { get; set; } = true;
}
