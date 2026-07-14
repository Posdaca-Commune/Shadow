using System.Reflection;
using System.Text.Json;

namespace Shadow.Abstractions;

public static class ShadowLocalizationResources
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static void RegisterFromDirectory(string localizationDirectory, IEnumerable<string>? cultureNames = null)
    {
        if (string.IsNullOrWhiteSpace(localizationDirectory) || !Directory.Exists(localizationDirectory))
        {
            return;
        }

        foreach (var cultureName in ResolveCultures(localizationDirectory, cultureNames))
        {
            var path = Path.Combine(localizationDirectory, $"{cultureName}.json");
            if (!File.Exists(path))
            {
                continue;
            }

            TryRegisterJson(cultureName, File.ReadAllText(path));
        }
    }

    public static void RegisterFromAssemblyDirectory(
        Assembly assembly,
        string relativeDirectory = "Localization",
        IEnumerable<string>? cultureNames = null)
    {
        var cultures = ResolveRequestedCultures(cultureNames);

        foreach (var directory in EnumerateCandidateDirectories(assembly, relativeDirectory))
        {
            RegisterFromDirectory(directory, cultures);
        }

        RegisterFromEmbeddedResources(assembly, relativeDirectory, cultures);
    }

    public static void RegisterFromEmbeddedResources(
        Assembly assembly,
        string relativeDirectory = "Localization",
        IEnumerable<string>? cultureNames = null)
    {
        var cultures = ResolveRequestedCultures(cultureNames);
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var cultureName in cultures)
        {
            var resourceName = resourceNames.FirstOrDefault(name =>
                name.Equals($"{assembly.GetName().Name}.{relativeDirectory}.{cultureName}.json", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith($".{relativeDirectory}.{cultureName}.json", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith($"{relativeDirectory}.{cultureName}.json", StringComparison.OrdinalIgnoreCase)
                || (name.EndsWith($".{cultureName}.json", StringComparison.OrdinalIgnoreCase)
                    && name.Contains(relativeDirectory, StringComparison.OrdinalIgnoreCase)));

            if (resourceName is null)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            TryRegisterJson(cultureName, reader.ReadToEnd());
        }
    }

    private static void TryRegisterJson(string cultureName, string json)
    {
        try
        {
            var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);
            if (strings is { Count: > 0 })
            {
                ShadowLocalizer.Instance.RegisterCulture(cultureName, strings);
            }
        }
        catch
        {
            // Invalid localization files should not prevent the host or plugins from starting.
        }
    }

    private static string[] ResolveRequestedCultures(IEnumerable<string>? cultureNames)
    {
        return cultureNames?
                   .Select(ShadowLocalizer.NormalizeCultureName)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray()
               ??
               [
                   ShadowLocalizer.DefaultCultureName,
                   ShadowLocalizer.EnglishCultureName,
               ];
    }

    private static IEnumerable<string> ResolveCultures(string localizationDirectory, IEnumerable<string>? cultureNames)
    {
        if (cultureNames is not null)
        {
            return ResolveRequestedCultures(cultureNames);
        }

        return Directory.EnumerateFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => ShadowLocalizer.NormalizeCultureName(name!))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateCandidateDirectories(Assembly assembly, string relativeDirectory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        void Consider(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            var fullPath = Path.GetFullPath(directory);
            if (seen.Add(fullPath))
            {
                candidates.Add(fullPath);
            }
        }

        var assemblyDirectory = Path.GetDirectoryName(assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            Consider(Path.Combine(assemblyDirectory, relativeDirectory));
        }

        Consider(Path.Combine(AppContext.BaseDirectory, relativeDirectory));
        Consider(Path.Combine(AppContext.BaseDirectory, "Plugins", "Hoi4Launcher", relativeDirectory));
        return candidates;
    }
}
