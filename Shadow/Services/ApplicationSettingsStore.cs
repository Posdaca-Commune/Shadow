using System;
using System.IO;
using System.Text.Json;
using Shadow.Abstractions;

namespace Shadow.Services;

internal sealed class ApplicationSettings
{
    public string Language { get; set; } = ShadowLocalizer.DefaultCultureName;
}

internal static class ApplicationSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string ApplicationDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Shadow");

    private static string SettingsPath => Path.Combine(ApplicationDataDirectory, "settings.json");

    public static ApplicationSettings Load()
    {
        try
        {
            Directory.CreateDirectory(ApplicationDataDirectory);
            if (!File.Exists(SettingsPath))
            {
                return new ApplicationSettings();
            }

            return JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(SettingsPath), SerializerOptions)
                   ?? new ApplicationSettings();
        }
        catch
        {
            return new ApplicationSettings();
        }
    }

    public static void Save(ApplicationSettings settings)
    {
        Directory.CreateDirectory(ApplicationDataDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
