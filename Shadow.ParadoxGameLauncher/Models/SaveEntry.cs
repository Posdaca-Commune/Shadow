using Shadow.ParadoxGameLauncher.Localization;

namespace Shadow.ParadoxGameLauncher.Models;

public sealed class SaveEntry
{
    public required string Name { get; init; }

    public required string FileName { get; init; }

    public required string FilePath { get; init; }

    public required string Extension { get; init; }

    public required DateTime LastWriteTime { get; init; }

    public required long SizeBytes { get; init; }

    public bool IsFolder { get; init; }

    public int FileCount { get; init; }

    public string LastWriteText => LastWriteTime.ToString("yyyy-MM-dd HH:mm");

    public string SizeText => FormatSize(SizeBytes);

    public string FileCountText =>
        ParadoxGameLauncherStrings.Format("Paradox.Saves.FileCount", FileCount);

    public bool HasMultipleFiles => IsFolder && FileCount > 1;

    public SaveMetadata? Metadata { get; set; }

    public bool HasMetadata => Metadata is { IsValid: true };

    public string? MetadataText
    {
        get
        {
            if (Metadata is not { IsValid: true } meta)
            {
                return null;
            }

            // For HOI4 (binary saves): show country tag + in-game date.
            // For Stellaris (ZIP saves): show save/empire name + in-game date.
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(meta.PlayerCountry))
                parts.Add(meta.PlayerCountry);
            else if (!string.IsNullOrWhiteSpace(meta.SaveName))
                parts.Add(meta.SaveName);
            if (!string.IsNullOrWhiteSpace(meta.Date))
                parts.Add(meta.Date);
            return parts.Count > 0 ? string.Join(" \u00b7 ", parts) : null;
        }
    }

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }
}
