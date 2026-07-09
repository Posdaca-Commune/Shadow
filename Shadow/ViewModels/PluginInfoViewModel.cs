using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Shadow.Abstractions;
using Shadow.Plugins;

namespace Shadow.ViewModels;

public sealed class PluginInfoViewModel : ViewModelBase
{
    internal PluginInfoViewModel(LoadedPlugin loadedPlugin)
    {
        var plugin = loadedPlugin.Plugin;
        var pluginType = plugin.GetType();
        var assembly = pluginType.Assembly;
        var assemblyName = assembly.GetName();

        Id = plugin.Id;
        DisplayName = string.IsNullOrWhiteSpace(plugin.DisplayName)
            ? plugin.Id
            : plugin.DisplayName;
        VersionLabel = ResolveVersion(assembly, assemblyName);
        AssemblyName = assemblyName.Name ?? "未知程序集";
        PluginType = pluginType.FullName ?? pluginType.Name;
        Location = string.IsNullOrWhiteSpace(assembly.Location)
            ? "未提供加载位置"
            : assembly.Location;
        DataDirectory = loadedPlugin.Context.PluginDataDirectory;
        NavigationItemCount = loadedPlugin.NavigationItems.Count;
        SettingsSectionCount = loadedPlugin.SettingsSections.Count;
        SupportsCommandLine = plugin is IShadowCommandPlugin;
        CapabilitySummary = CreateCapabilitySummary();
        Summary = $"{Id} · {CapabilitySummary}";
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string VersionLabel { get; }

    public string AssemblyName { get; }

    public string PluginType { get; }

    public string Location { get; }

    public string DataDirectory { get; }

    public int NavigationItemCount { get; }

    public int SettingsSectionCount { get; }

    public bool SupportsCommandLine { get; }

    public string CapabilitySummary { get; }

    public string Summary { get; }

    private string CreateCapabilitySummary()
    {
        var capabilities = new List<string>();
        if (NavigationItemCount > 0)
        {
            capabilities.Add($"{NavigationItemCount} 个导航入口");
        }

        if (SettingsSectionCount > 0)
        {
            capabilities.Add($"{SettingsSectionCount} 个设置页");
        }

        if (SupportsCommandLine)
        {
            capabilities.Add("命令行");
        }

        return capabilities.Count == 0
            ? "未声明扩展入口"
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

        return assemblyName.Version?.ToString() ?? "版本未知";
    }
}
