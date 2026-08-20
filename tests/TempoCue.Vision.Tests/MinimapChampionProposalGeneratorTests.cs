namespace TempoCue.Vision.Tests;

public sealed class MinimapChampionProposalGeneratorTests
{
    [Fact]
    public void Create_CentersPortraitSearchRegionOnColorContour()
    {
        MinimapMarkerCandidate[] candidates =
        [
            new(42, 30, 9, 11, MinimapMarkerAffiliation.Enemy, 0.72)
        ];

        var proposals = MinimapChampionProposalGenerator.Create(100, 80, candidates);

        var proposal = Assert.Single(proposals);
        Assert.Equal((34, 23, 25, 25),
            (proposal.X, proposal.Y, proposal.Width, proposal.Height));
        Assert.Equal(MinimapMarkerAffiliation.Enemy, proposal.Affiliation);
    }

    [Fact]
    public void Create_RemovesNearbyContoursOfSamePortrait()
    {
        MinimapMarkerCandidate[] candidates =
        [
            new(40, 30, 8, 8, MinimapMarkerAffiliation.Ally, 0.80),
            new(44, 33, 7, 7, MinimapMarkerAffiliation.Ally, 0.60)
        ];

        var proposals = MinimapChampionProposalGenerator.Create(100, 80, candidates);

        Assert.Single(proposals);
    }
}
