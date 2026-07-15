using System;
using Avalonia;
using Shadow.Abstractions;
using Shadow.Localization;
using Shadow.Plugins;
using Shadow.Services;

namespace Shadow;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        InitializeLocalization();
        var commandLine = ShadowCommandLine.Parse(args);
        if (!string.IsNullOrWhiteSpace(commandLine.Command))
        {
            var pluginCatalog = PluginCatalog.LoadDefault();
            var result = pluginCatalog.ExecuteCommand(commandLine);
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                var output = result.ExitCode == 0 ? Console.Out : Console.Error;
                output.WriteLine(result.Message);
            }

            return result.ExitCode;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    internal static ApplicationSettings InitializeLocalization()
    {
        ShadowStringResources.Register();
        var settings = ApplicationSettingsStore.Load();
        ShadowLocalizer.Instance.CultureName = settings.Language;
        return settings;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .With(ShadowFontOptions.Create())
            .LogToTrace();
}

