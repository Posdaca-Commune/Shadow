using System.Collections.Generic;
using System.Linq;
using Shadow.Abstractions;

namespace Shadow.Plugins;

internal sealed class LoadedPlugin
{
    public LoadedPlugin(
        IShadowPlugin plugin,
        IShadowHostContext context)
    {
        Plugin = plugin;
        Context = context;
    }

    public IShadowPlugin Plugin { get; }

    public IShadowHostContext Context { get; }

    public IReadOnlyList<ShadowNavigationItem> NavigationItems => _navigationItems ??= Plugin
        .CreateNavigationItems(Context)
        .ToArray();

    public IReadOnlyList<ShadowSettingsSection> SettingsSections => _settingsSections ??= Plugin
        .CreateSettingsSections(Context)
        .ToArray();

    private IReadOnlyList<ShadowNavigationItem>? _navigationItems;

    private IReadOnlyList<ShadowSettingsSection>? _settingsSections;
}
