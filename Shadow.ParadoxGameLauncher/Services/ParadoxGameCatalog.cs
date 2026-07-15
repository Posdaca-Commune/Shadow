using Shadow.ParadoxGameLauncher.Models;

namespace Shadow.ParadoxGameLauncher.Services;

public static class ParadoxGameCatalog
{
    public static IReadOnlyList<ParadoxGameDefinition> Games { get; } =
    [
        new()
        {
            Id = "hoi4",
            DisplayName = "Hearts of Iron IV",
            DocumentsFolderName = "Hearts of Iron IV",
            ExecutableFileName = "hoi4.exe",
            SteamAppId = "394360",
            SteamFolderNames = ["Hearts of Iron IV", "Hearts of Iron IV/"],
        },
        new()
        {
            Id = "ck3",
            DisplayName = "Crusader Kings III",
            DocumentsFolderName = "Crusader Kings III",
            ExecutableFileName = "ck3.exe",
            SteamAppId = "1158310",
            SteamFolderNames = ["Crusader Kings III"],
        },
        new()
        {
            Id = "eu4",
            DisplayName = "Europa Universalis IV",
            DocumentsFolderName = "Europa Universalis IV",
            ExecutableFileName = "eu4.exe",
            SteamAppId = "236850",
            SteamFolderNames = ["Europa Universalis IV"],
        },
        new()
        {
            Id = "stellaris",
            DisplayName = "Stellaris",
            DocumentsFolderName = "Stellaris",
            ExecutableFileName = "stellaris.exe",
            SteamAppId = "281990",
            SteamFolderNames = ["Stellaris"],
        },
        new()
        {
            Id = "vic3",
            DisplayName = "Victoria 3",
            DocumentsFolderName = "Victoria 3",
            ExecutableFileName = "victoria3.exe",
            SteamAppId = "529340",
            SteamFolderNames = ["Victoria 3"],
        },
        new()
        {
            Id = "imperator",
            DisplayName = "Imperator: Rome",
            DocumentsFolderName = "ImperatorRome",
            ExecutableFileName = "ImperatorRome.exe",
            SteamAppId = "859580",
            SteamFolderNames = ["ImperatorRome", "Imperator Rome"],
        },
    ];

    public static ParadoxGameDefinition Default => Games[0];

    public static ParadoxGameDefinition GetById(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return Default;
        }

        return Games.FirstOrDefault(game =>
                   string.Equals(game.Id, gameId, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }
}
