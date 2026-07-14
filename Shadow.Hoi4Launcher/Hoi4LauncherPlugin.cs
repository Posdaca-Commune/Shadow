using Shadow.Hoi4Launcher.Models;
using Shadow.Abstractions;
using Shadow.Hoi4Launcher.Localization;
using Shadow.Hoi4Launcher.Services;
using Shadow.Hoi4Launcher.ViewModels;

namespace Shadow.Hoi4Launcher;

public sealed class Hoi4LauncherPlugin : IShadowCommandPlugin
{
    private const string LaunchCommand = "hoi4.launch";

    public Hoi4LauncherPlugin()
    {
        Hoi4LauncherStrings.Register();
    }

    public string Id => "Shadow.Hoi4Launcher";

    public string DisplayName => LocalizedText.Key("Hoi4.Plugin.DisplayName");

    public IReadOnlyList<ShadowNavigationItem> CreateNavigationItems(IShadowHostContext context)
    {
        Hoi4LauncherStrings.Register();
        var configuration = Hoi4LauncherConfiguration.Load(context.PluginDataDirectory);
        var service = new Hoi4LauncherService(configuration);
        var viewModel = new Hoi4LauncherViewModel(configuration, service, context);

        return
        [
            new ShadowNavigationItem(
                $"{Id}.Launcher",
                LocalizedText.Key("Hoi4.Nav.Title"),
                LocalizedText.Key("Hoi4.Nav.Description"),
                "Play",
                viewModel),
        ];
    }

    public IReadOnlyList<ShadowSettingsSection> CreateSettingsSections(IShadowHostContext context)
    {
        return [];
    }

    public ShadowCommandResult ExecuteCommand(ShadowCommandContext context)
    {
        if (!string.Equals(context.Command, LaunchCommand, StringComparison.OrdinalIgnoreCase))
        {
            return ShadowCommandResult.NotHandled;
        }

        try
        {
            Hoi4LauncherStrings.Register();
            var configuration = Hoi4LauncherConfiguration.Load(context.HostContext.PluginDataDirectory);
            var service = new Hoi4LauncherService(configuration);
            var mods = service.DiscoverMods();
            configuration.PlaysetStore.SaveModIndex(mods);

            var playsets = configuration.PlaysetStore.LoadPlaysets();
            var playset = ResolvePlayset(context.Options, configuration, playsets);
            if (playset is null)
            {
                return ShadowCommandResult.Failure(Hoi4LauncherStrings.Get("Hoi4.Command.NoPlayset"));
            }

            var missingModIds = FindMissingEnabledModIds(playset, mods);
            if (missingModIds.Count > 0 && !HasFlag(context.Options, "allow-missing-mods"))
            {
                return ShadowCommandResult.Failure(
                    Hoi4LauncherStrings.Format("Hoi4.Command.MissingMods", string.Join(", ", missingModIds.Take(8))));
            }

            var dlcs = service.DiscoverDlcs();
            service.ApplyPlayset(playset, mods, ApplyDlcSelection(playset, dlcs));
            var process = service.StartGame();
            configuration.State.SelectedPlaysetId = playset.Id;
            configuration.Save();

            return ShadowCommandResult.Success(
                Hoi4LauncherStrings.Format("Hoi4.Command.Launched", playset.Name, process.Id));
        }
        catch (Exception ex)
        {
            return ShadowCommandResult.Failure(ex.Message);
        }
    }

    private static Playset? ResolvePlayset(
        IReadOnlyDictionary<string, string> options,
        Hoi4LauncherConfiguration configuration,
        IReadOnlyList<Playset> playsets)
    {
        var playsetId = GetOption(options, "playset-id", "playsetId", "playset");
        if (!string.IsNullOrWhiteSpace(playsetId))
        {
            return playsets.FirstOrDefault(playset =>
                string.Equals(playset.Id, playsetId, StringComparison.OrdinalIgnoreCase));
        }

        return playsets.FirstOrDefault(playset => string.Equals(
                   playset.Id,
                   configuration.State.SelectedPlaysetId,
                   StringComparison.OrdinalIgnoreCase))
               ?? playsets.FirstOrDefault();
    }

    private static IReadOnlyList<DlcEntry> ApplyDlcSelection(Playset playset, IReadOnlyList<DlcEntry> dlcs)
    {
        var disabledDlcIds = playset.DisabledDlcIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var dlc in dlcs)
        {
            dlc.IsEnabled = !disabledDlcIds.Contains(dlc.Id);
        }

        return dlcs;
    }

    private static IReadOnlyList<string> FindMissingEnabledModIds(Playset playset, IReadOnlyList<ModEntry> mods)
    {
        var knownMods = Hoi4ModIdentity.BuildLookup(mods);
        return playset.EnabledModIds
            .Where(modId => !knownMods.ContainsKey(modId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetOption(IReadOnlyDictionary<string, string> options, params string[] names)
    {
        foreach (var name in names)
        {
            if (options.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static bool HasFlag(IReadOnlyDictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out var value)
               && (string.IsNullOrWhiteSpace(value)
                   || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
