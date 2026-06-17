namespace Shadow.Abstractions;

public interface IShadowPlugin
{
    string Id { get; }

    string DisplayName { get; }

    IReadOnlyList<ShadowNavigationItem> CreateNavigationItems(IShadowHostContext context);

    IReadOnlyList<ShadowSettingsSection> CreateSettingsSections(IShadowHostContext context);
}

public interface IShadowCommandPlugin : IShadowPlugin
{
    ShadowCommandResult ExecuteCommand(ShadowCommandContext context);
}

public interface IShadowHostContext
{
    string ApplicationDataDirectory { get; }

    string PluginDataDirectory { get; }

    void ShutdownApplication();
}

public sealed record ShadowNavigationItem(
    string Key,
    string Title,
    string Description,
    string IconKey,
    object Content);

public sealed record ShadowSettingsSection(
    string Key,
    string Title,
    string Description,
    string IconKey,
    object Content);

public sealed record ShadowCommandContext(
    IShadowHostContext HostContext,
    string Command,
    IReadOnlyDictionary<string, string> Options);

public sealed record ShadowCommandResult(
    bool Handled,
    int ExitCode,
    string Message)
{
    public static ShadowCommandResult NotHandled { get; } = new(false, 1, string.Empty);

    public static ShadowCommandResult Success(string message = "") => new(true, 0, message);

    public static ShadowCommandResult Failure(string message, int exitCode = 1) => new(true, exitCode, message);
}
