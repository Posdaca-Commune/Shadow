using System.IO.Compression;
using System.Text;
using Shadow.ParadoxGameLauncher.Models;
using Shadow.ParadoxGameLauncher.Services;
using Xunit;

namespace Shadow.Tests.Services;

public class SaveMetadataParserTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static SaveEntry FileEntry(string filePath) => new()
    {
        Name = System.IO.Path.GetFileName(filePath),
        FileName = System.IO.Path.GetFileName(filePath),
        FilePath = filePath,
        Extension = System.IO.Path.GetExtension(filePath),
        LastWriteTime = DateTime.Now,
        SizeBytes = 0,
    };

    private static byte[] Hoi4BinarySave(string playerTag)
    {
        // Header per the parser's expectations: "HOI4bin5" prefix, a binary
        // separator byte, then the player tag token.
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.ASCII.GetBytes("HOI4bin5"));
        bytes.Add(0x01);
        bytes.AddRange(Encoding.ASCII.GetBytes(playerTag));
        bytes.AddRange(Enumerable.Repeat((byte)0x00, 64));
        bytes.AddRange(Encoding.ASCII.GetBytes("1939_11_29_12"));
        return [.. bytes];
    }

    private static byte[] ZipSaveWithMeta(string metaText)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("meta");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(metaText);
        }

        return stream.ToArray();
    }

    [Fact]
    public void Parse_Hoi4BinarySave_ExtractsPlayerTagAndDateFromFileName()
    {
        var filePath = _temp.CreateFile("GER_1939_11_29_12.hoi4", Hoi4BinarySave("GER"));

        var metadata = SaveMetadataParser.Parse(FileEntry(filePath));

        Assert.NotNull(metadata);
        Assert.Equal("GER", metadata.PlayerCountry);
        Assert.Equal("1939.11.29", metadata.Date);
    }

    [Fact]
    public void Parse_Hoi4BinarySave_TooShortFileReturnsNull()
    {
        var filePath = _temp.CreateFile("short.hoi4", [0x48, 0x4F, 0x49]);

        Assert.Null(SaveMetadataParser.Parse(FileEntry(filePath)));
    }

    [Fact]
    public void Parse_UnknownHeaderReturnsNull()
    {
        var filePath = _temp.CreateFile("unknown.sav", Encoding.ASCII.GetBytes("JUNK0000" + new string('\0', 64)));

        Assert.Null(SaveMetadataParser.Parse(FileEntry(filePath)));
    }

    [Fact]
    public void Parse_MissingFileReturnsNull()
    {
        var filePath = System.IO.Path.Combine(_temp.Path, "does-not-exist.hoi4");

        Assert.Null(SaveMetadataParser.Parse(FileEntry(filePath)));
    }

    [Fact]
    public void Parse_ZipSave_ReadsMetaEntry()
    {
        var contents = ZipSaveWithMeta(
            """
            version="1.16.8"
            name="My Empire"
            date="2420.05.01"
            """);
        var filePath = _temp.CreateFile("united_earth_2420.sav", contents);

        var metadata = SaveMetadataParser.Parse(FileEntry(filePath));

        Assert.NotNull(metadata);
        Assert.Equal("My Empire", metadata.SaveName);
        Assert.Equal("2420.05.01", metadata.Date);
        Assert.Equal("1.16.8", metadata.GameVersion);
    }

    [Fact]
    public void Parse_ZipSaveWithoutMetaEntryReturnsNull()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("gamestate");
        }

        var filePath = _temp.CreateFile("empty.sav", stream.ToArray());

        Assert.Null(SaveMetadataParser.Parse(FileEntry(filePath)));
    }

    [Fact]
    public void Parse_Eu4TextSave_ExtractsMetaFields()
    {
        var contents = Encoding.UTF8.GetBytes(
            "EU4txt\n" +
            "date=\"1601.03.12\"\n" +
            "name=\"Ironman Osmanli\"\n" +
            "version=\"1.37.5\"\n");
        var filePath = _temp.CreateFile("ironman_osmanli.eu4", contents);

        var metadata = SaveMetadataParser.Parse(FileEntry(filePath));

        Assert.NotNull(metadata);
        Assert.Equal("1601.03.12", metadata.Date);
        Assert.Equal("Ironman Osmanli", metadata.SaveName);
        Assert.Equal("1.37.5", metadata.GameVersion);
    }

    [Fact]
    public void Parse_FolderSave_UsesMostRecentFile()
    {
        var folder = System.IO.Path.Combine(_temp.Path, "stellaris-save");
        Directory.CreateDirectory(folder);
        var older = ZipSaveWithMeta("name=\"Old Empire\"\ndate=\"2200.01.01\"\n");
        var newer = ZipSaveWithMeta("name=\"New Empire\"\ndate=\"2300.01.01\"\n");
        var olderPath = System.IO.Path.Combine(folder, "old.sav");
        var newerPath = System.IO.Path.Combine(folder, "new.sav");
        File.WriteAllBytes(olderPath, older);
        File.WriteAllBytes(newerPath, newer);
        var cutoff = DateTime.Now;
        File.SetLastWriteTime(olderPath, cutoff.AddHours(-2));
        File.SetLastWriteTime(newerPath, cutoff.AddHours(-1));

        var saveEntry = new SaveEntry
        {
            Name = "stellaris-save",
            FileName = "stellaris-save",
            FilePath = folder,
            Extension = ".sav",
            LastWriteTime = cutoff,
            SizeBytes = 0,
            IsFolder = true,
        };

        var metadata = SaveMetadataParser.Parse(saveEntry);

        Assert.NotNull(metadata);
        Assert.Equal("New Empire", metadata.SaveName);
        Assert.Equal("2300.01.01", metadata.Date);
    }

    [Fact]
    public void Parse_EmptyFolderSaveReturnsNull()
    {
        var folder = System.IO.Path.Combine(_temp.Path, "empty-save");
        Directory.CreateDirectory(folder);

        var saveEntry = new SaveEntry
        {
            Name = "empty-save",
            FileName = "empty-save",
            FilePath = folder,
            Extension = ".sav",
            LastWriteTime = DateTime.Now,
            SizeBytes = 0,
            IsFolder = true,
        };

        Assert.Null(SaveMetadataParser.Parse(saveEntry));
    }
}
