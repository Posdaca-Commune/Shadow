using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Shadow.Abstractions;

namespace Shadow.Plugins;

internal sealed class PluginCatalog
{
    private PluginCatalog(IReadOnlyList<LoadedPlugin> plugins)
    {
        Plugins = plugins;
        NavigationItems = plugins.SelectMany(plugin => plugin.NavigationItems).ToArray();
        SettingsSections = plugins.SelectMany(plugin => plugin.SettingsSections).ToArray();
    }

    public IReadOnlyList<LoadedPlugin> Plugins { get; }

    public IReadOnlyList<ShadowNavigationItem> NavigationItems { get; }

    public IReadOnlyList<ShadowSettingsSection> SettingsSections { get; }

    public static PluginCatalog LoadDefault()
    {
        var hostDirectory = Path.GetDirectoryName(typeof(PluginCatalog).Assembly.Location)
                            ?? AppContext.BaseDirectory;
        var pluginsDirectory = Path.Combine(hostDirectory, "Plugins");
        var applicationDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Shadow");

        Directory.CreateDirectory(applicationDataDirectory);
        Directory.CreateDirectory(pluginsDirectory);

        var loadedPlugins = new List<LoadedPlugin>();
        foreach (var assemblyPath in EnumeratePluginAssemblies(pluginsDirectory))
        {
            TryLoadPluginAssembly(assemblyPath, applicationDataDirectory, loadedPlugins);
        }

        return new PluginCatalog(loadedPlugins);
    }

    public ShadowCommandResult ExecuteCommand(ShadowCommandLine commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine.Command))
        {
            return ShadowCommandResult.NotHandled;
        }

        foreach (var loadedPlugin in Plugins)
        {
            if (loadedPlugin.Plugin is not IShadowCommandPlugin commandPlugin)
            {
                continue;
            }

            var result = commandPlugin.ExecuteCommand(new ShadowCommandContext(
                loadedPlugin.Context,
                commandLine.Command,
                commandLine.Options));
            if (result.Handled)
            {
                return result;
            }
        }

        return ShadowCommandResult.Failure($"未知 Shadow 命令：{commandLine.Command}");
    }

    private static IEnumerable<string> EnumeratePluginAssemblies(string pluginsDirectory)
    {
        return Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), "Shadow.Abstractions.dll",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}runtimes{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetFileName(path).StartsWith("Shadow.", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    private static void TryLoadPluginAssembly(
        string assemblyPath,
        string applicationDataDirectory,
        ICollection<LoadedPlugin> loadedPlugins)
    {
        try
        {
            var loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            foreach (var pluginType in FindPluginTypes(assembly))
            {
                if (Activator.CreateInstance(pluginType) is not IShadowPlugin plugin)
                {
                    continue;
                }

                var pluginDataDirectory = Path.Combine(applicationDataDirectory, "Plugins", plugin.Id);
                Directory.CreateDirectory(pluginDataDirectory);

                var context = new ShadowHostContext(applicationDataDirectory, pluginDataDirectory);
                loadedPlugins.Add(new LoadedPlugin(
                    plugin,
                    context));
            }
        }
        catch (Exception ex)
        {
            WritePluginLoadError(applicationDataDirectory, assemblyPath, ex);
        }
    }

    private static void WritePluginLoadError(string applicationDataDirectory, string assemblyPath, Exception exception)
    {
        try
        {
            var logPath = Path.Combine(applicationDataDirectory, "plugin-load-errors.log");
            File.AppendAllText(
                logPath,
                $"{DateTimeOffset.Now:u} {assemblyPath}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging failure should not prevent the host from starting.
        }
    }

    private static IEnumerable<Type> FindPluginTypes(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => typeof(IShadowPlugin).IsAssignableFrom(type)
                           && type is { IsAbstract: false, IsInterface: false }
                           && type.GetConstructor(Type.EmptyTypes) is not null);
    }
}
