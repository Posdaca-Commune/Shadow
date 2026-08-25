using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Shadow.ParadoxGameLauncher.Models;

namespace Shadow.ParadoxGameLauncher.Services;

/// <summary>
/// Extracts lightweight metadata (in-game date, country tag, save name) from
/// Paradox save files without fully parsing the gamestate.
///
/// ZIP-based saves (Stellaris, CK3, EU4, Vic3) store a plain-text "meta" entry
/// inside the archive. Binary saves (HOI4) embed the player country tag in the
/// file header; the in-game date is parsed from the filename when present.
/// </summary>
public static class SaveMetadataParser
{
    public static SaveMetadata? Parse(SaveEntry save)
    {
        if (save.IsFolder)
        {
            return ParseFolderSave(save);
        }

        return ParseFileSave(save.FilePath);
    }

    private static SaveMetadata? ParseFolderSave(SaveEntry save)
    {
        // Folder-based saves (Stellaris): find the latest .sav file and parse it.
        if (!Directory.Exists(save.FilePath))
        {
            return null;
        }

        var latestFile = Directory.EnumerateFiles(save.FilePath)
            .Select(filePath => new FileInfo(filePath))
            .OrderByDescending(info => info.LastWriteTime)
            .FirstOrDefault();
        if (latestFile is null)
        {
            return null;
        }

        return ParseFileSave(latestFile.FullName);
    }

    private static SaveMetadata? ParseFileSave(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var stream = File.OpenRead(filePath);
        Span<byte> header = stackalloc byte[4];
        if (stream.Read(header) < 4)
        {
            return null;
        }

        // ZIP (PK\x03\x04) — Stellaris, EU4, CK3, Vic3
        if (header[0] == 0x50 && header[1] == 0x4B)
        {
            return ParseZipSave(stream);
        }

        // HOI4 binary ("HOI4bin")
        var headString = Encoding.ASCII.GetString(header);
        if (headString is "HOI4")
        {
            stream.Position = 0;
            return ParseHoi4Binary(stream);
        }

        // EU4 binary ("EU4bin") or EU4 text ("EU4txt")
        if (headString is "EU4b" or "EU4t")
        {
            stream.Position = 0;
            return ParseEu4Save(stream);
        }

        return null;
    }

    // ── ZIP-based saves ──────────────────────────────────────────────

    private static SaveMetadata? ParseZipSave(FileStream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var metaEntry = archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Name, "meta", StringComparison.OrdinalIgnoreCase));
        if (metaEntry is null)
        {
            return null;
        }

        using var reader = new StreamReader(metaEntry.Open(), Encoding.UTF8);
        var metaText = reader.ReadToEnd();
        return ParseClausewitzMeta(metaText);
    }

    // ── Clausewitz text meta parsing ─────────────────────────────────

    private static SaveMetadata ParseClausewitzMeta(string text)
    {
        var date = ExtractQuotedValue(text, "date");
        var saveName = ExtractQuotedValue(text, "name");
        var version = ExtractQuotedValue(text, "version");

        return new SaveMetadata
        {
            Date = date,
            SaveName = saveName,
            GameVersion = version,
        };
    }

    /// <summary>
    /// Extracts the value of <c>key="value"</c> from a Clausewitz text block.
    /// </summary>
    private static string? ExtractQuotedValue(string text, string key)
    {
        var match = Regex.Match(text, $@"^{Regex.Escape(key)}\s*=\s*""([^""]*)""", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    // ── HOI4 binary parsing ──────────────────────────────────────────

    private static SaveMetadata? ParseHoi4Binary(FileStream stream)
    {
        // Scan the first ~500 bytes for recognizable ASCII tokens.
        var buffer = new byte[500];
        stream.Position = 0;
        var read = stream.Read(buffer, 0, buffer.Length);
        if (read < 50)
        {
            return null;
        }

        var tokens = ExtractAlphanumericTokens(buffer, read);

        // HOI4 header layout (after "HOI4bin5"):
        //   <player_tag> | <ideology> | <difficulty> | <version_string> | <filename> ...
        // We extract the player country tag (2-4 uppercase letters).
        var playerTag = tokens.FirstOrDefault(t => t.Length is >= 2 and <= 4 && IsAllUpper(t));

        // In-game date is encoded in autosave filenames: TAG_YYYY_MM_DD_HH.hoi4
        var date = TryParseDateFromFilePath(stream.Name) ?? TryParseDateFromTokens(tokens);

        return new SaveMetadata
        {
            Date = date,
            PlayerCountry = playerTag,
        };
    }

    // ── EU4 binary/text parsing ──────────────────────────────────────

    private static SaveMetadata? ParseEu4Save(FileStream stream)
    {
        var header = new byte[6];
        var headerRead = stream.Read(header, 0, header.Length);
        if (headerRead < 6)
        {
            return null;
        }
        var headerStr = Encoding.ASCII.GetString(header);

        if (headerStr.StartsWith("EU4txt", StringComparison.Ordinal))
        {
            // Text-format EU4 save: read fully and extract fields.
            using var textReader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
            var text = textReader.ReadToEnd();
            return ParseClausewitzMeta(text);
        }

        if (headerStr.StartsWith("EU4bin", StringComparison.Ordinal))
        {
            // Binary EU4: scan for date near the top.
            var buffer = new byte[500];
            var read = stream.Read(buffer, 0, buffer.Length);
            var tokens = ExtractAlphanumericTokens(buffer, read);
            var date = tokens.FirstOrDefault(t => Regex.IsMatch(t, @"^\d{4}\.\d{1,2}\.\d{1,2}$"));
            return new SaveMetadata { Date = date };
        }

        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Splits binary data into word-like tokens, keeping only letters, digits,
    /// underscores, dots, spaces and parentheses. Non-alphanumeric bytes (which
    /// include Paradox binary operators) act as token separators.
    /// </summary>
    private static List<string> ExtractAlphanumericTokens(byte[] buffer, int length)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            var b = buffer[i];
            if (b is >= 0x41 and <= 0x5A      // A-Z
                or >= 0x61 and <= 0x7A       // a-z
                or >= 0x30 and <= 0x39       // 0-9
                or 0x5F or 0x2E or 0x20      // _ . space
                or 0x28 or 0x29)            // ( )
            {
                sb.Append((char)b);
            }
            else
            {
                AddToken(sb, tokens);
            }
        }
        AddToken(sb, tokens);
        return tokens;
    }

    private static void AddToken(StringBuilder sb, List<string> tokens)
    {
        if (sb.Length >= 2)
        {
            var trimmed = sb.ToString().Trim();
            if (trimmed.Length >= 2)
            {
                tokens.Add(trimmed);
            }
        }
        sb.Clear();
    }

    private static bool IsAllUpper(string value)
    {
        return value.Length >= 2 && value.All(c => c is >= 'A' and <= 'Z');
    }

    /// <summary>
    /// Parses an in-game date from a file path whose name encodes it,
    /// e.g. <c>PRC_1939_11_29_12.hoi4</c> -> "1939.11.29".
    /// </summary>
    private static string? TryParseDateFromFilePath(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return TryParseDateFromTokens([fileName]);
    }

    private static string? TryParseDateFromTokens(IReadOnlyList<string> tokens)
    {
        foreach (var token in tokens)
        {
            var match = Regex.Match(token, @"(\d{4})[._](\d{1,2})[._](\d{1,2})");
            if (match.Success)
            {
                return $"{match.Groups[1].Value}.{int.Parse(match.Groups[2].Value)}.{int.Parse(match.Groups[3].Value)}";
            }
        }
        return null;
    }
}
