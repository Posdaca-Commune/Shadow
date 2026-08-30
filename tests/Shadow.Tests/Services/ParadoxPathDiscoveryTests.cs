using Shadow.ParadoxGameLauncher.Models;
using Shadow.ParadoxGameLauncher.Services;
using Xunit;

namespace Shadow.Tests.Services;

public class ParadoxPathDiscoveryTests
{
    private static ParadoxGameDefinition Game(string id = "ck3") => ParadoxGameCatalog.GetById(id);

    // ── libraryfolders.vdf parsing ───────────────────────────────────

    [Fact]
    public void ExtractLibraryPaths_ReadsMultipleLibraries()
    {
        const string vdf = """
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"C:\\Program Files (x86)\\Steam"
            	}
            	"1"
            	{
            		"path"		"D:\\SteamLibrary"
            	}
            }
            """;

        var paths = ParadoxPathDiscovery.ExtractLibraryPaths(vdf);

        Assert.Equal(2, paths.Count);
        Assert.Equal(@"C:\Program Files (x86)\Steam", paths[0]);
        Assert.Equal(@"D:\SteamLibrary", paths[1]);
    }

    [Fact]
    public void ExtractLibraryPaths_KeepsUnixStylePaths()
    {
        const string vdf = """
            "libraryfolders"
            {
            	"1"
            	{
            		"path"		"/home/user/.local/share/Steam"
            	}
            }
            """;

        var paths = ParadoxPathDiscovery.ExtractLibraryPaths(vdf);

        var path = Assert.Single(paths);
        Assert.Equal("/home/user/.local/share/Steam", path);
    }

    [Fact]
    public void ExtractLibraryPaths_EmptyWhenNoPathEntries()
    {
        const string vdf = """
            "libraryfolders"
            {
            	"0"
            	{
            		"size"		"123"
            	}
            }
            """;

        Assert.Empty(ParadoxPathDiscovery.ExtractLibraryPaths(vdf));
    }

    // ── user directory candidates ────────────────────────────────────

    [Fact]
    public void GetUserDirectoryCandidates_WindowsListsDocumentsVariants()
    {
        var game = Game("hoi4");

        var candidates = ParadoxPathDiscovery.GetUserDirectoryCandidates(
            game, userProfile: @"C:\Users\demo", xdgDataHome: null, isWindows: true).ToList();

        Assert.Equal(3, candidates.Count);
        Assert.Equal(
            Path.Combine(@"C:\Users\demo", "OneDrive", "Documents", "Paradox Interactive", "Hearts of Iron IV"),
            candidates[1]);
        Assert.Equal(
            Path.Combine(@"C:\Users\demo", "Documents", "Paradox Interactive", "Hearts of Iron IV"),
            candidates[2]);
    }

    [Fact]
    public void GetUserDirectoryCandidates_LinuxPrefersXdgDataHome()
    {
        var game = Game("hoi4");

        var candidates = ParadoxPathDiscovery.GetUserDirectoryCandidates(
            game, userProfile: "/home/demo", xdgDataHome: "/home/demo/xdg-data", isWindows: false).ToList();

        Assert.Equal(3, candidates.Count);
        Assert.Equal(
            Path.Combine("/home/demo/xdg-data", "Paradox Interactive", "Hearts of Iron IV"),
            candidates[1]);
        Assert.Equal(
            Path.Combine("/home/demo", ".var", "app", "com.valvesoftware.Steam", "data",
                "Paradox Interactive", "Hearts of Iron IV"),
            candidates[2]);
    }

    [Fact]
    public void GetUserDirectoryCandidates_LinuxFallsBackToLocalShare()
    {
        var game = Game("hoi4");

        var candidates = ParadoxPathDiscovery.GetUserDirectoryCandidates(
            game, userProfile: "/home/demo", xdgDataHome: null, isWindows: false).ToList();

        Assert.Equal(
            Path.Combine("/home/demo", ".local", "share", "Paradox Interactive", "Hearts of Iron IV"),
            candidates[1]);
    }

    // ── executable resolution ────────────────────────────────────────

    [Fact]
    public void ResolveExecutableFromDirectory_FindsNestedLinuxBinary()
    {
        using var temp = new TempDirectory();
        var installDir = System.IO.Path.Combine(temp.Path, "steamapps", "common", "Crusader Kings III");
        var binariesDir = System.IO.Path.Combine(installDir, "binaries");
        Directory.CreateDirectory(binariesDir);
        var binaryPath = System.IO.Path.Combine(binariesDir, "ck3");
        File.WriteAllText(binaryPath, "binary");

        var resolved = ParadoxPathDiscovery.ResolveExecutableFromDirectory(Game("ck3"), installDir);

        Assert.Equal(binaryPath, resolved);
    }

    [Fact]
    public void ResolveExecutableFromDirectory_FindsExtensionlessBinaryEvenOnWindowsHost()
    {
        using var temp = new TempDirectory();
        var installDir = System.IO.Path.Combine(temp.Path, "steamapps", "common", "Hearts of Iron IV");
        Directory.CreateDirectory(installDir);
        var binaryPath = System.IO.Path.Combine(installDir, "hoi4");
        File.WriteAllText(binaryPath, "binary");

        var resolved = ParadoxPathDiscovery.ResolveExecutableFromDirectory(Game("hoi4"), installDir);

        Assert.Equal(binaryPath, resolved);
    }

    [Fact]
    public void ResolveExecutableFromDirectory_ReturnsNullForMissingBinary()
    {
        using var temp = new TempDirectory();
        var installDir = System.IO.Path.Combine(temp.Path, "steamapps", "common", "Victoria 3");
        Directory.CreateDirectory(installDir);

        Assert.Null(ParadoxPathDiscovery.ResolveExecutableFromDirectory(Game("vic3"), installDir));
    }

    // ── executable name candidates ───────────────────────────────────

    [Fact]
    public void GetExecutableFileNames_WindowsUsesExeName()
    {
        var candidates = Game("hoi4").GetExecutableFileNames(isWindows: true);

        var name = Assert.Single(candidates);
        Assert.Equal("hoi4.exe", name);
    }

    [Fact]
    public void GetExecutableFileNames_LinuxTriesNativeNamesThenGameId()
    {
        var candidates = Game("vic3").GetExecutableFileNames(isWindows: false).ToList();

        Assert.Equal(["bin/victoria3", "vic3"], candidates);
    }
}
