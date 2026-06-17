namespace Shadow.Abstractions;

public interface IShadowPlugin
{
    string Id { get; }

    string DisplayName { get; }

    IReadOnlyList<ShadowNavigationItem> CreateNavigationItems(IShadowHostContext context);

    IReadOnlyList<ShadowSettingsSection> CreateSettingsSections(IShadowHostContext context);
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
