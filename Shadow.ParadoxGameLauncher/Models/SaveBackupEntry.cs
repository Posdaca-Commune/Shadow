namespace Shadow.ParadoxGameLauncher.Models;

public sealed class SaveBackupEntry
{
    public required string FileName { get; init; }

    public required string FilePath { get; init; }

    public required DateTime CreatedTime { get; init; }

    public required long SizeBytes { get; init; }

    public string CreatedText => CreatedTime.ToString("yyyy-MM-dd HH:mm:ss");

    public string SizeText => SaveEntry.FormatSize(SizeBytes);
}
