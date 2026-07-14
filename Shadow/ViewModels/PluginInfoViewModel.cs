using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Shadow.Abstractions;
using Shadow.Plugins;

namespace Shadow.ViewModels;

public sealed class PluginInfoViewModel : ViewModelBase
{
    private readonly IShadowPlugin _plugin;
    private readonly string _assemblyName;
    private readonly string _location;
    private readonly string _versionLabel;

    internal PluginInfoViewModel(LoadedPlugin loadedPlugin)
    {
        var plugin = loadedPlugin.Plugin;
        _plugin = plugin;
        var pluginType = plugin.GetType();
        var assembly = pluginType.Assembly;
        var assemblyName = assembly.GetName();

        Id = plugin.Id;
        _versionLabel = ResolveVersion(assembly, assemblyName);
        _assemblyName = assemblyName.Name ?? string.Empty;
        PluginType = pluginType.FullName ?? pluginType.Name;
        _location = assembly.Location;
        DataDirectory = loadedPlugin.Context.PluginDataDirectory;
        NavigationItemCount = loadedPlugin.NavigationItems.Count;
        SettingsSectionCount = loadedPlugin.SettingsSections.Count;
        SupportsCommandLine = plugin is IShadowCommandPlugin;
        ShadowLocalizer.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(VersionLabel));
            OnPropertyChanged(nameof(AssemblyName));
            OnPropertyChanged(nameof(Location));
            OnPropertyChanged(nameof(CapabilitySummary));
            OnPropertyChanged(nameof(Summary));
        };
    }

    public string Id { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(_plugin.DisplayName)
        ? _plugin.Id
        : LocalizedText.Resolve(_plugin.DisplayName);

    public string VersionLabel => string.IsNullOrWhiteSpace(_versionLabel)
        ? Localizer["Shadow.Plugins.UnknownVersion"]
        : LocalizedText.Resolve(_versionLabel);

    public string AssemblyName => string.IsNullOrWhiteSpace(_assemblyName)
        ? Localizer["Shadow.Plugins.UnknownAssembly"]
        : _assemblyName;

    public string PluginType { get; }

    public string Location => string.IsNullOrWhiteSpace(_location)
        ? Localizer["Shadow.Plugins.MissingLocation"]
        : _location;

    public string DataDirectory { get; }

    public int NavigationItemCount { get; }

    public int SettingsSectionCount { get; }

    public bool SupportsCommandLine { get; }

    public string CapabilitySummary => CreateCapabilitySummary();

    public string Summary => $"{Id} · {CapabilitySummary}";

    private string CreateCapabilitySummary()
    {
        var capabilities = new List<string>();
        if (NavigationItemCount > 0)
        {
            capabilities.Add(Localizer.Format("Shadow.Plugins.NavigationEntryCount", NavigationItemCount));
        }

        if (SettingsSectionCount > 0)
        {
            capabilities.Add(Localizer.Format("Shadow.Plugins.SettingsSectionCount", SettingsSectionCount));
        }

        if (SupportsCommandLine)
        {
            capabilities.Add(Localizer["Shadow.Plugins.CommandLine"]);
        }

        return capabilities.Count == 0
            ? Localizer["Shadow.Plugins.NoDeclaredEntrypoints"]
            : string.Join(" / ", capabilities);
    }

    private static string ResolveVersion(Assembly assembly, AssemblyName assemblyName)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        if (!string.IsNullOrWhiteSpace(assembly.Location))
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);
            if (!string.IsNullOrWhiteSpace(fileVersion.ProductVersion))
            {
                return fileVersion.ProductVersion;
            }

            if (!string.IsNullOrWhiteSpace(fileVersion.FileVersion))
            {
                return fileVersion.FileVersion;
            }
        }

        return assemblyName.Version?.ToString() ?? string.Empty;
    }
}

