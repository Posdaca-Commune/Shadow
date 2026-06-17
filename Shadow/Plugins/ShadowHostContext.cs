using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Shadow.Abstractions;

namespace Shadow.Plugins;

internal sealed class ShadowHostContext : IShadowHostContext
{
    public ShadowHostContext(string applicationDataDirectory, string pluginDataDirectory)
    {
        ApplicationDataDirectory = applicationDataDirectory;
        PluginDataDirectory = pluginDataDirectory;
    }

    public string ApplicationDataDirectory { get; }

    public string PluginDataDirectory { get; }

    public void ShutdownApplication()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
