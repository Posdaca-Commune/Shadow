using System.Reflection;
using System.Threading;
using Shadow.Abstractions;

namespace Shadow.Localization;

internal static class ShadowStringResources
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        ShadowLocalizationResources.RegisterFromAssemblyDirectory(
            Assembly.GetExecutingAssembly(),
            relativeDirectory: "Localization",
            cultureNames:
            [
                ShadowLocalizer.DefaultCultureName,
                ShadowLocalizer.EnglishCultureName,
            ]);
    }
}
