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

    public required int BackupCount { get; init; }

    public string LastWriteText => LastWriteTime.ToString("yyyy-MM-dd HH:mm");

    public string SizeText => FormatSize(SizeBytes);

    public string BackupCountText =>
        ParadoxGameLauncherStrings.Format("Paradox.Saves.BackupCount", BackupCount);

    public bool HasBackups => BackupCount > 0;

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
