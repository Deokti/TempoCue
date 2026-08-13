using TempoCue.Core;

namespace TempoCue.Core.Tests;

public sealed class RiotIdTests
{
    [Fact]
    public void Create_TrimsPartsAndBuildsDisplayName()
    {
        var riotId = RiotId.Create(" Summoner A ", " EUW ");

        Assert.Equal("Summoner A", riotId.GameName);
        Assert.Equal("EUW", riotId.TagLine);
        Assert.Equal("Summoner A#EUW", riotId.DisplayName);
    }

    [Theory]
    [InlineData("", "EUW")]
    [InlineData("Summoner", "")]
    [InlineData("Summoner#Other", "EUW")]
    [InlineData("Summoner", "EU\nW")]
    public void TryCreate_RejectsInvalidParts(string gameName, string tagLine)
    {
        var created = RiotId.TryCreate(gameName, tagLine, out var riotId);

        Assert.False(created);
        Assert.Null(riotId);
    }
}
