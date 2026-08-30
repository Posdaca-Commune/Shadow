using Shadow.Plugins;
using Xunit;

namespace Shadow.Tests.Plugins;

public class ShadowCommandLineTests
{
    [Theory]
    [InlineData("launch")]
    [InlineData("pdxgamelauncher")]
    [InlineData("paradoxgamelauncher")]
    [InlineData("paradox.launch")]
    [InlineData("paradox-launch")]
    [InlineData("hoi4.launch")]
    [InlineData("PARADOX.LAUNCH")]
    public void Parse_NormalizesKnownCommandAliases(string input)
    {
        var commandLine = ShadowCommandLine.Parse([input]);

        Assert.Equal("paradox.launch", commandLine.Command);
    }

    [Fact]
    public void Parse_KeepsUnknownCommand()
    {
        var commandLine = ShadowCommandLine.Parse(["custom.command"]);

        Assert.Equal("custom.command", commandLine.Command);
    }

    [Fact]
    public void Parse_NoCommand_WhenArgsEmpty()
    {
        var commandLine = ShadowCommandLine.Parse([]);

        Assert.Null(commandLine.Command);
        Assert.Empty(commandLine.Options);
        Assert.Empty(commandLine.Positionals);
    }

    [Fact]
    public void Parse_ReadsSpaceSeparatedOptionValue()
    {
        var commandLine = ShadowCommandLine.Parse(["paradox.launch", "-game", "hoi4"]);

        Assert.Equal("hoi4", commandLine.Options["game"]);
    }

    [Fact]
    public void Parse_ReadsEqualsSeparatedOptionValue()
    {
        var commandLine = ShadowCommandLine.Parse(["paradox.launch", "--game=hoi4"]);

        Assert.Equal("hoi4", commandLine.Options["game"]);
    }

    [Fact]
    public void Parse_OptionLookupIsCaseInsensitive()
    {
        var commandLine = ShadowCommandLine.Parse(["paradox.launch", "-GAME", "hoi4"]);

        Assert.Equal("hoi4", commandLine.Options["game"]);
    }

    [Fact]
    public void Parse_GameOptionFansOutToAliases()
    {
        var commandLine = ShadowCommandLine.Parse(["paradox.launch", "-g", "hoi4"]);

        Assert.Equal("hoi4", commandLine.Options["game"]);
        Assert.Equal("hoi4", commandLine.Options["game-id"]);
        Assert.Equal("hoi4", commandLine.Options["gameId"]);
    }

    [Fact]
    public void Parse_FlagWithoutValueIsTrue()
    {
        var commandLine = ShadowCommandLine.Parse(["paradox.launch", "-debug", "-allow-missing-mods"]);

        Assert.Equal("true", commandLine.Options["debug"]);
        Assert.Equal("true", commandLine.Options["allow-missing-mods"]);
    }

    [Fact]
    public void Parse_CommandOptionSetsCommand()
    {
        var commandLine = ShadowCommandLine.Parse(["--command", "paradox.launch"]);

        Assert.Equal("paradox.launch", commandLine.Command);
    }

    [Fact]
    public void Parse_ExtraTokensBecomePositionals()
    {
        var commandLine = ShadowCommandLine.Parse(["paradox.launch", "-debug", "extra-one", "extra-two"]);

        Assert.Equal(["extra-one", "extra-two"], commandLine.Positionals);
    }

    [Fact]
    public void Parse_MapsPositionalsToGameAndPlayset()
    {
        var commandLine = ShadowCommandLine.Parse(["launch", "hoi4", "my-playset"]);

        Assert.Equal("paradox.launch", commandLine.Command);
        Assert.Equal("hoi4", commandLine.Options["game"]);
        Assert.Equal("my-playset", commandLine.Options["playset"]);
    }

    [Fact]
    public void Parse_ExplicitGameOptionWinsOverPositional()
    {
        var commandLine = ShadowCommandLine.Parse(["launch", "-game", "eu4", "hoi4"]);

        Assert.Equal("eu4", commandLine.Options["game"]);
        // Only one positional remains, which is below the two-argument threshold
        // for playset promotion, so no playset option is set.
        Assert.False(commandLine.Options.ContainsKey("playset"));
    }
}
