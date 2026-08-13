using TempoCue.App.Profiles;
using TempoCue.Core;

namespace TempoCue.App.Tests;

public sealed class LeagueOfGraphsProfileUriBuilderTests
{
    [Fact]
    public void Build_UsesClientRegionAndEscapesRiotId()
    {
        var uri = LeagueOfGraphsProfileUriBuilder.Build(
            RiotId.Create("Summoner A", "EUW"),
            "EUW");

        Assert.Equal(
            "https://www.leagueofgraphs.com/summoner/euw/Summoner+A-EUW",
            uri?.AbsoluteUri);
    }

    [Fact]
    public void Build_DoesNotGuessUnsupportedRegion()
    {
        var uri = LeagueOfGraphsProfileUriBuilder.Build(
            RiotId.Create("Summoner", "TEST"),
            "UNKNOWN");

        Assert.Null(uri);
    }
}
