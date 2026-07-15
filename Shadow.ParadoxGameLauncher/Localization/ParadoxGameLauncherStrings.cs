using System.Reflection;
using Shadow.Abstractions;

namespace Shadow.ParadoxGameLauncher.Localization;

internal static class ParadoxGameLauncherStrings
{
    private static readonly object Gate = new();
    private static bool _loaded;

    public static void Register()
    {
        lock (Gate)
        {
            if (_loaded && HasCultureKey(ShadowLocalizer.EnglishCultureName, "Paradox.Action.LaunchGame")
                        && HasCultureKey(ShadowLocalizer.DefaultCultureName, "Paradox.Action.LaunchGame"))
            {
                return;
            }

            var cultures = new[]
            {
                ShadowLocalizer.DefaultCultureName,
                ShadowLocalizer.EnglishCultureName,
            };

            // Prefer embedded resources first so plugin ALC / install layout cannot miss files.
            ShadowLocalizationResources.RegisterFromEmbeddedResources(
                Assembly.GetExecutingAssembly(),
                relativeDirectory: "Localization",
                cultureNames: cultures);

            ShadowLocalizationResources.RegisterFromAssemblyDirectory(
                Assembly.GetExecutingAssembly(),
                relativeDirectory: "Localization",
                cultureNames: cultures);

            _loaded = HasCultureKey(ShadowLocalizer.DefaultCultureName, "Paradox.Action.LaunchGame")
                      || HasCultureKey(ShadowLocalizer.EnglishCultureName, "Paradox.Action.LaunchGame");
        }
    }

    public static string Get(string key)
    {
        Register();
        return ShadowLocalizer.Instance[key];
    }

    public static string Format(string key, params object[] args)
    {
        Register();
        return ShadowLocalizer.Instance.Format(key, args);
    }

    private static bool HasCultureKey(string cultureName, string key)
    {
        return ShadowLocalizer.Instance.HasCultureKey(cultureName, key);
    }
}
