using Shadow.Abstractions;
using Shadow.Hoi4Launcher.Services;
using Shadow.Hoi4Launcher.ViewModels;

namespace Shadow.Hoi4Launcher;

public sealed class Hoi4LauncherPlugin : IShadowPlugin
{
    public string Id => "Shadow.Hoi4Launcher";

    public string DisplayName => "HOI4 启动器";

    public IReadOnlyList<ShadowNavigationItem> CreateNavigationItems(IShadowHostContext context)
    {
        var configuration = Hoi4LauncherConfiguration.Load(context.PluginDataDirectory);
        var service = new Hoi4LauncherService(configuration);
        var viewModel = new Hoi4LauncherViewModel(configuration, service, context);

        return
        [
            new ShadowNavigationItem(
                $"{Id}.Launcher",
                "HOI4 启动器",
                "游戏、模组、DLC 和播放集",
                "Play",
                viewModel),
        ];
    }

    public IReadOnlyList<ShadowSettingsSection> CreateSettingsSections(IShadowHostContext context)
    {
        return [];
    }
}
