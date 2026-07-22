using System;
using System.IO;
using System.Text.Json;
using Avalonia.Media;
using Shadow.Abstractions;
using Shadow.Models;

namespace Shadow.Services;

internal sealed class ApplicationSettings
{
    public string Language { get; set; } = ShadowLocalizer.DefaultCultureName;

    public string ThemeMode { get; set; } = nameof(AppThemeMode.System);

    public string Backdrop { get; set; } = nameof(WindowBackdropKind.Mica);

    public string AccentColor { get; set; } = "#5B7CFA";

    public bool UseSystemAccentColor { get; set; } = true;

    public bool ShowCompactSidebar { get; set; }

    public bool EnableAnimations { get; set; } = true;
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

    public static void ApplyToPersonalization(ApplicationSettings settings, PersonalizationOptions personalization)
    {
        personalization.ThemeMode = Enum.TryParse<AppThemeMode>(settings.ThemeMode, ignoreCase: true, out var themeMode)
            ? themeMode
            : AppThemeMode.System;
        personalization.Backdrop = Enum.TryParse<WindowBackdropKind>(settings.Backdrop, ignoreCase: true, out var backdrop)
            ? backdrop
            : WindowBackdropKind.Mica;
        personalization.AccentColor = TryParseColor(settings.AccentColor, Color.Parse("#5B7CFA"));
        personalization.UseSystemAccentColor = settings.UseSystemAccentColor;
        personalization.ShowCompactSidebar = settings.ShowCompactSidebar;
        personalization.EnableAnimations = settings.EnableAnimations;
    }

    public static void CaptureFromPersonalization(ApplicationSettings settings, PersonalizationOptions personalization)
    {
        settings.ThemeMode = personalization.ThemeMode.ToString();
        settings.Backdrop = personalization.Backdrop.ToString();
        settings.AccentColor = $"#{personalization.AccentColor.R:X2}{personalization.AccentColor.G:X2}{personalization.AccentColor.B:X2}";
        settings.UseSystemAccentColor = personalization.UseSystemAccentColor;
        settings.ShowCompactSidebar = personalization.ShowCompactSidebar;
        settings.EnableAnimations = personalization.EnableAnimations;
    }

    private static Color TryParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return Color.Parse(value);
        }
        catch
        {
            return fallback;
        }
    }
}
