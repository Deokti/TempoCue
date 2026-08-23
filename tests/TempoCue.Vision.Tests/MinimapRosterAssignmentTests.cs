namespace TempoCue.Vision.Tests;

public sealed class MinimapRosterAssignmentTests
{
    [Fact]
    public void Select_AssignsCloseAlternativeAfterStrongerRegionClaimsInitialLeader()
    {
        var diana = CreateReference("Diana", MinimapMarkerAffiliation.Ally);
        var sett = CreateReference("Sett", MinimapMarkerAffiliation.Ally);
        var sion = CreateReference("Sion", MinimapMarkerAffiliation.Enemy);
        MinimapRosterCandidateScore[] scores =
        [
            CreateCandidateScore(
                index: 0,
                x: 10,
                y: 10,
                affiliation: MinimapMarkerAffiliation.Ally,
                negativeSimilarity: 0.70,
                (diana, 0.91),
                (sett, 0.70),
                (sion, 0.68)),
            CreateCandidateScore(
                index: 1,
                x: 50,
                y: 50,
                affiliation: MinimapMarkerAffiliation.Ally,
                negativeSimilarity: 0.73,
                (diana, 0.884),
                (sett, 0.866),
                (sion, 0.80))
        ];

        var selected = MinimapRosterAssignment.Select(scores);

        Assert.Collection(
            selected,
            match => Assert.Equal("Diana", match.ChampionId),
            match => Assert.Equal("Sett", match.ChampionId));
    }

    [Fact]
    public void Select_DoesNotAssignDistantRemainingParticipantToAmbiguousRegion()
    {
        var diana = CreateReference("Diana", MinimapMarkerAffiliation.Ally);
        var sett = CreateReference("Sett", MinimapMarkerAffiliation.Ally);
        MinimapRosterCandidateScore[] scores =
        [
            CreateCandidateScore(
                index: 0,
                x: 10,
                y: 10,
                affiliation: MinimapMarkerAffiliation.Ally,
                negativeSimilarity: 0.70,
                (diana, 0.91),
                (sett, 0.70)),
            CreateCandidateScore(
                index: 1,
                x: 50,
                y: 50,
                affiliation: MinimapMarkerAffiliation.Ally,
                negativeSimilarity: 0.70,
                (diana, 0.88),
                (sett, 0.82))
        ];

        var selected = MinimapRosterAssignment.Select(scores);

        var match = Assert.Single(selected);
        Assert.Equal("Diana", match.ChampionId);
    }

    private static MinimapRosterCandidateScore CreateCandidateScore(
        int index,
        int x,
        int y,
        MinimapMarkerAffiliation affiliation,
        double negativeSimilarity,
        params (MinimapChampionReference Reference, double Similarity)[] scores) =>
        new(
            index,
            new MinimapChampionMarkerCandidate(x, y, 25, 25, affiliation, 0.70),
            scores
                .OrderByDescending(static score => score.Similarity)
                // Масштаб на назначение состава не влияет: он записан для замера.
                .Select(static score => new MinimapRosterReferenceScore(
                    score.Reference,
                    score.Similarity,
                    ScaleIndex: 0))
                .ToArray(),
            negativeSimilarity);

    private static MinimapChampionReference CreateReference(
        string championId,
        MinimapMarkerAffiliation affiliation) =>
        new(
            championId,
            affiliation,
            new byte[
                MinimapChampionReference.ImageSide *
                MinimapChampionReference.ImageSide *
                MinimapChampionReference.BytesPerPixel]);
}
