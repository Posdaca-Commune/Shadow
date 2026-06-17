using System.Globalization;

namespace Shadow.Hoi4Launcher.Models;

public sealed class GameSettings
{
    public string Language { get; set; } = "l_english";

    public int DisplayIndex { get; set; }

    public int FullscreenMode { get; set; } = 1;

    public int ResolutionWidth { get; set; } = 1920;

    public int ResolutionHeight { get; set; } = 1080;

    public int RefreshRate { get; set; } = 60;

    public bool VSync { get; set; } = true;

    public float MasterVolume { get; set; } = 0.75f;

    public float MusicVolume { get; set; } = 0.5f;

    public float EffectsVolume { get; set; } = 0.75f;

    public float InterfaceVolume { get; set; } = 0.75f;

    public static GameSettings Load(string settingsPath)
    {
        var settings = new GameSettings();
        if (!File.Exists(settingsPath))
        {
            return settings;
        }

        var lines = File.ReadAllLines(settingsPath);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (TryReadScalar(line, "language", out var language))
            {
                settings.Language = language.Trim('"');
            }
            else if (TryReadScalar(line, "display_index", out var displayIndex)
                     && int.TryParse(displayIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var displayIndexValue))
            {
                settings.DisplayIndex = displayIndexValue;
            }
            else if (TryReadScalar(line, "fullScreen", out var fullScreen)
                     && TryReadBoolean(fullScreen, out var fullScreenValue))
            {
                settings.FullscreenMode = fullScreenValue ? 1 : 0;
            }
            else if (TryReadScalar(line, "borderless", out var borderless)
                     && TryReadBoolean(borderless, out var borderlessValue)
                     && borderlessValue)
            {
                settings.FullscreenMode = 2;
            }
            else if (TryReadScalar(line, "vsync", out var vsync)
                     && TryReadBoolean(vsync, out var vsyncValue))
            {
                settings.VSync = vsyncValue;
            }
            else if (TryReadScalar(line, "refreshRate", out var refreshRate)
                     && int.TryParse(refreshRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var refreshRateValue))
            {
                settings.RefreshRate = refreshRateValue;
            }
            else if (line.StartsWith("size=", StringComparison.Ordinal))
            {
                ReadSizeBlock(lines, ref i, settings);
            }
            else if (line.StartsWith("volume=", StringComparison.Ordinal))
            {
                ReadVolumeBlock(lines, ref i, settings);
            }
            else if (TryReadScalar(line, "master_volume", out var masterVolume))
            {
                settings.MasterVolume = ReadVolume(masterVolume, settings.MasterVolume);
            }
            else if (TryReadScalar(line, "music_volume", out var musicVolume))
            {
                settings.MusicVolume = ReadVolume(musicVolume, settings.MusicVolume);
            }
            else if (TryReadScalar(line, "sound_fx_volume", out var effectsVolume))
            {
                settings.EffectsVolume = ReadVolume(effectsVolume, settings.EffectsVolume);
            }
            else if (TryReadScalar(line, "ambient_volume", out var interfaceVolume))
            {
                settings.InterfaceVolume = ReadVolume(interfaceVolume, settings.InterfaceVolume);
            }
        }

        return settings;
    }

    public void Save(string settingsPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, ToSettingsText());
    }

    private string ToSettingsText()
    {
        var fullScreen = FullscreenMode == 1;
        var borderless = FullscreenMode == 2;

        return string.Join(Environment.NewLine,
            "force_pow2_textures=no",
            "graphics={",
            "    size={",
            $"        x={ResolutionWidth}",
            $"        y={ResolutionHeight}",
            "    }",
            "    min_gui={",
            $"        x={ResolutionWidth}",
            $"        y={ResolutionHeight}",
            "    }",
            $"    display_index={DisplayIndex}",
            $"    refreshRate={RefreshRate}",
            $"    max_refresh_rate={RefreshRate}",
            $"    fullScreen={WriteBoolean(fullScreen)}",
            $"    borderless={WriteBoolean(borderless)}",
            $"    vsync={WriteBoolean(VSync)}",
            "}",
            $"language=\"{Language}\"",
            $"master_volume={WriteVolume(MasterVolume)}",
            $"music_volume={WriteVolume(MusicVolume)}",
            $"sound_fx_volume={WriteVolume(EffectsVolume)}",
            $"ambient_volume={WriteVolume(InterfaceVolume)}",
            string.Empty);
    }

    private static bool TryReadScalar(string line, string key, out string value)
    {
        var prefix = key + "=";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = line[prefix.Length..].Trim();
        return true;
    }

    private static void ReadSizeBlock(string[] lines, ref int index, GameSettings settings)
    {
        var block = lines[index];
        while (!block.Contains('}', StringComparison.Ordinal) && index + 1 < lines.Length)
        {
            block += " " + lines[++index].Trim();
        }

        settings.ResolutionWidth = ReadIntValue(block, "x", settings.ResolutionWidth);
        settings.ResolutionHeight = ReadIntValue(block, "y", settings.ResolutionHeight);
    }

    private static void ReadVolumeBlock(string[] lines, ref int index, GameSettings settings)
    {
        while (index + 1 < lines.Length)
        {
            var line = lines[++index].Trim();
            if (line.StartsWith('}'))
            {
                break;
            }

            if (TryReadScalar(line, "master", out var master))
            {
                settings.MasterVolume = ReadVolume(master, settings.MasterVolume);
            }
            else if (TryReadScalar(line, "music", out var music))
            {
                settings.MusicVolume = ReadVolume(music, settings.MusicVolume);
            }
            else if (TryReadScalar(line, "effects", out var effects))
            {
                settings.EffectsVolume = ReadVolume(effects, settings.EffectsVolume);
            }
            else if (TryReadScalar(line, "interface", out var interfaceVolume))
            {
                settings.InterfaceVolume = ReadVolume(interfaceVolume, settings.InterfaceVolume);
            }
        }
    }

    private static int ReadIntValue(string source, string key, int fallback)
    {
        var marker = key + "=";
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return fallback;
        }

        var valueStart = markerIndex + marker.Length;
        var valueEnd = valueStart;
        while (valueEnd < source.Length && char.IsDigit(source[valueEnd]))
        {
            valueEnd++;
        }

        return int.TryParse(source[valueStart..valueEnd], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static bool TryReadBoolean(string value, out bool result)
    {
        result = false;
        return value.Trim().ToLowerInvariant() switch
        {
            "yes" or "true" or "1" => SetBooleanResult(true, out result),
            "no" or "false" or "0" => SetBooleanResult(false, out result),
            _ => false,
        };
    }

    private static bool SetBooleanResult(bool value, out bool result)
    {
        result = value;
        return true;
    }

    private static string WriteBoolean(bool value)
    {
        return value ? "yes" : "no";
    }

    private static float ReadVolume(string value, float fallback)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return fallback;
        }

        return Math.Clamp(result > 1f ? result / 100f : result, 0f, 1f);
    }

    private static string WriteVolume(float value)
    {
        return (Math.Clamp(value, 0f, 1f) * 100f).ToString("0.000000", CultureInfo.InvariantCulture);
    }
}
