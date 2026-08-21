using System.Text;
using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

public sealed class MinimapReplayGroundTruthTests
{
    [Fact]
    public async Task SaveAndLoad_PreserveRegionsAndAllowances()
    {
        var groundTruth = new MinimapReplayGroundTruth(
            [
                new MinimapGroundTruthRegion(
                    100,
                    120,
                    25,
                    25,
                    MinimapGroundTruthKind.Champion,
                    "Anivia",
                    MinimapMarkerAffiliation.Enemy),
                new MinimapGroundTruthRegion(
                    40,
                    200,
                    20,
                    18,
                    MinimapGroundTruthKind.MinionWave,
                    affiliation: MinimapMarkerAffiliation.Ally),
                new MinimapGroundTruthRegion(60, 80, 16, 16, MinimapGroundTruthKind.Tower)
            ],
            allowedFalsePositives: 2,
            allowedMissedChampions: 1);
        using var stream = new MemoryStream();

        await MinimapReplayGroundTruthStore.SaveAsync(stream, groundTruth);
        stream.Position = 0;
        var loaded = await MinimapReplayGroundTruthStore.LoadAsync(stream);

        Assert.Equal(3, loaded.Regions.Count);
        Assert.Equal(2, loaded.AllowedFalsePositives);
        Assert.Equal(1, loaded.AllowedMissedChampions);
        var champion = Assert.Single(
            loaded.Regions,
            region => region.Kind == MinimapGroundTruthKind.Champion);
        Assert.Equal("Anivia", champion.ChampionId);
        Assert.Equal(MinimapMarkerAffiliation.Enemy, champion.Affiliation);
        Assert.Equal(100, champion.X);
    }

    [Fact]
    public async Task Load_RejectsUnknownSchemaVersion()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"schemaVersion": 99, "regions": []}"""));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => MinimapReplayGroundTruthStore.LoadAsync(stream));
    }

    [Fact]
    public async Task Load_RejectsChampionRegionWithoutChampionId()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """
            {
              "schemaVersion": 1,
              "regions": [
                { "x": 1, "y": 1, "width": 10, "height": 10, "kind": "Champion" }
              ]
            }
            """));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => MinimapReplayGroundTruthStore.LoadAsync(stream));
    }

    [Fact]
    public async Task Load_RejectsBrokenJson()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{not json"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => MinimapReplayGroundTruthStore.LoadAsync(stream));
    }

    [Fact]
    public void Constructor_RejectsNegativeAllowances()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MinimapReplayGroundTruth([], allowedFalsePositives: -1));
    }

    [Fact]
    public void Constructor_RejectsRegionOutsideCanonicalFrame()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MinimapGroundTruthRegion(310, 10, 25, 25, MinimapGroundTruthKind.Tower));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MinimapGroundTruthRegion(10, 300, 16, 25, MinimapGroundTruthKind.Tower));
    }
}
