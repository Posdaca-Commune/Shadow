using System.Collections.Generic;
using Shadow.Abstractions;

namespace Shadow.Plugins;

internal sealed class LoadedPlugin
{
    public LoadedPlugin(
        IShadowPlugin plugin,
        IReadOnlyList<ShadowNavigationItem> navigationItems,
        IReadOnlyList<ShadowSettingsSection> settingsSections)
    {
        Plugin = plugin;
        NavigationItems = navigationItems;
        SettingsSections = settingsSections;
    }

    public IShadowPlugin Plugin { get; }

    public IReadOnlyList<ShadowNavigationItem> NavigationItems { get; }

    public IReadOnlyList<ShadowSettingsSection> SettingsSections { get; }
}
