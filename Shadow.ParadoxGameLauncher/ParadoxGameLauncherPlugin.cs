using Shadow.ParadoxGameLauncher.Models;
using Shadow.Abstractions;
using Shadow.ParadoxGameLauncher.Localization;
using Shadow.ParadoxGameLauncher.Services;
using Shadow.ParadoxGameLauncher.ViewModels;

namespace Shadow.ParadoxGameLauncher;

public sealed class ParadoxGameLauncherPlugin : IShadowCommandPlugin
{
    private const string LaunchCommand = "paradox.launch";

    public ParadoxGameLauncherPlugin()
    {
        ParadoxGameLauncherStrings.Register();
    }

    public string Id => "Shadow.ParadoxGameLauncher";

    public string DisplayName => LocalizedText.Key("Paradox.Plugin.DisplayName");

    public IReadOnlyList<ShadowNavigationItem> CreateNavigationItems(IShadowHostContext context)
    {
        ParadoxGameLauncherStrings.Register();
        var configuration = ParadoxGameLauncherConfiguration.Load(context.PluginDataDirectory);
        var service = new ParadoxGameLauncherService(configuration);
        var viewModel = new ParadoxGameLauncherViewModel(configuration, service, context);

        return
        [
            new ShadowNavigationItem(
                $"{Id}.Launcher",
                LocalizedText.Key("Paradox.Nav.Title"),
                LocalizedText.Key("Paradox.Nav.Description"),
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
            ParadoxGameLauncherStrings.Register();
            var configuration = ParadoxGameLauncherConfiguration.Load(context.HostContext.PluginDataDirectory);
            var gameId = GetOption(context.Options, "game", "game-id", "gameId");
            if (!string.IsNullOrWhiteSpace(gameId))
            {
                configuration.SelectGame(gameId);
            }

            var service = new ParadoxGameLauncherService(configuration);
            var mods = service.DiscoverMods();
            configuration.PlaysetStore.SaveModIndex(mods);

            var playsets = configuration.PlaysetStore.LoadPlaysets();
            var playset = ResolvePlayset(context.Options, configuration, playsets);
            if (playset is null)
            {
                return ShadowCommandResult.Failure(ParadoxGameLauncherStrings.Get("Paradox.Command.NoPlayset"));
            }

            var missingModIds = FindMissingEnabledModIds(playset, mods);
            if (missingModIds.Count > 0 && !HasFlag(context.Options, "allow-missing-mods"))
            {
                return ShadowCommandResult.Failure(
                    ParadoxGameLauncherStrings.Format("Paradox.Command.MissingMods", string.Join(", ", missingModIds.Take(8))));
            }

            var dlcs = service.DiscoverDlcs();
            service.ApplyPlayset(playset, mods, ApplyDlcSelection(playset, dlcs));
            var process = service.StartGame();
            configuration.SelectedPlaysetId = playset.Id;
            configuration.Save();

            return ShadowCommandResult.Success(
                ParadoxGameLauncherStrings.Format(
                    "Paradox.Command.Launched",
                    configuration.SelectedGame.DisplayName,
                    playset.Name,
                    process.Id));
        }
        catch (Exception ex)
        {
            return ShadowCommandResult.Failure(ex.Message);
        }
    }

    private static Playset? ResolvePlayset(
        IReadOnlyDictionary<string, string> options,
        ParadoxGameLauncherConfiguration configuration,
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
                   configuration.SelectedPlaysetId,
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
        var knownMods = ParadoxModIdentity.BuildLookup(mods);
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
