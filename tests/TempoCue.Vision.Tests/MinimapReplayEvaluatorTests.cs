using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

public sealed class MinimapReplayEvaluatorTests
{
    [Fact]
    public void Evaluate_MatchesConfirmationToLabeledChampion()
    {
        var outcome = CreateOutcome(
            CreateCandidate(100, 120, "Anivia", MinimapMarkerAffiliation.Enemy));
        var groundTruth = new MinimapReplayGroundTruth(
            [
                new MinimapGroundTruthRegion(
                    98,
                    118,
                    25,
                    25,
                    MinimapGroundTruthKind.Champion,
                    "Anivia",
                    MinimapMarkerAffiliation.Enemy)
            ]);

        var evaluation = MinimapReplayEvaluator.Evaluate(outcome, groundTruth);

        Assert.Single(evaluation.ConfirmedChampions);
        Assert.Empty(evaluation.MissedChampions);
        Assert.Empty(evaluation.FalsePositives);
        Assert.Equal(1, evaluation.Precision);
        Assert.Equal(1, evaluation.Recall);
        Assert.True(evaluation.SatisfiesAllowances(groundTruth));
    }

    [Fact]
    public void Evaluate_ReportsTowerConfirmationAsExplainedFalsePositive()
    {
        var outcome = CreateOutcome(
            CreateCandidate(60, 80, "Pyke", MinimapMarkerAffiliation.Ally));
        var groundTruth = new MinimapReplayGroundTruth(
            [new MinimapGroundTruthRegion(58, 78, 16, 16, MinimapGroundTruthKind.Tower)]);

        var evaluation = MinimapReplayEvaluator.Evaluate(outcome, groundTruth);

        var falsePositive = Assert.Single(evaluation.FalsePositives);
        Assert.Equal(
            MinimapGroundTruthKind.Tower,
            Assert.IsType<MinimapGroundTruthRegion>(falsePositive.NearestRegion).Kind);
        Assert.Equal(0, evaluation.Precision);
        Assert.False(evaluation.SatisfiesAllowances(groundTruth));
    }

    [Fact]
    public void Evaluate_TreatsWrongIdentityAsFalsePositiveAndMiss()
    {
        var outcome = CreateOutcome(
            CreateCandidate(100, 120, "Pyke", MinimapMarkerAffiliation.Enemy));
        var groundTruth = new MinimapReplayGroundTruth(
            [
                new MinimapGroundTruthRegion(
                    100,
                    120,
                    25,
                    25,
                    MinimapGroundTruthKind.Champion,
                    "Anivia",
                    MinimapMarkerAffiliation.Enemy)
            ]);

        var evaluation = MinimapReplayEvaluator.Evaluate(outcome, groundTruth);

        Assert.Empty(evaluation.ConfirmedChampions);
        Assert.Single(evaluation.MissedChampions);
        var falsePositive = Assert.Single(evaluation.FalsePositives);
        Assert.Equal(
            MinimapGroundTruthKind.Champion,
            Assert.IsType<MinimapGroundTruthRegion>(falsePositive.NearestRegion).Kind);
    }

    [Fact]
    public void Evaluate_DoesNotMatchConfirmationOutsideCenterDistance()
    {
        var outcome = CreateOutcome(
            CreateCandidate(100, 120, "Anivia", MinimapMarkerAffiliation.Enemy));
        var groundTruth = new MinimapReplayGroundTruth(
            [
                new MinimapGroundTruthRegion(
                    160,
                    120,
                    25,
                    25,
                    MinimapGroundTruthKind.Champion,
                    "Anivia",
                    MinimapMarkerAffiliation.Enemy)
            ]);

        var evaluation = MinimapReplayEvaluator.Evaluate(outcome, groundTruth);

        Assert.Empty(evaluation.ConfirmedChampions);
        Assert.Single(evaluation.MissedChampions);
        var falsePositive = Assert.Single(evaluation.FalsePositives);
        Assert.Null(falsePositive.NearestRegion);
    }

    [Fact]
    public void Evaluate_RespectsRecordedAllowancesForKnownBehavior()
    {
        var outcome = CreateOutcome(
            CreateCandidate(60, 80, "Pyke", MinimapMarkerAffiliation.Ally));
        var groundTruth = new MinimapReplayGroundTruth(
            [
                new MinimapGroundTruthRegion(58, 78, 16, 16, MinimapGroundTruthKind.Tower),
                new MinimapGroundTruthRegion(
                    200,
                    200,
                    25,
                    25,
                    MinimapGroundTruthKind.Champion,
                    "Anivia",
                    MinimapMarkerAffiliation.Enemy)
            ],
            allowedFalsePositives: 1,
            allowedMissedChampions: 1);

        var evaluation = MinimapReplayEvaluator.Evaluate(outcome, groundTruth);

        Assert.Single(evaluation.FalsePositives);
        Assert.Single(evaluation.MissedChampions);
        Assert.True(evaluation.SatisfiesAllowances(groundTruth));
    }

    private static MinimapReplayOutcome CreateOutcome(
        params MinimapChampionMarkerCandidate[] championCandidates) =>
        new([], championCandidates.Length, championCandidates, 0.9);

    private static MinimapChampionMarkerCandidate CreateCandidate(
        int x,
        int y,
        string championId,
        MinimapMarkerAffiliation affiliation) =>
        new(
            x,
            y,
            Width: 25,
            Height: 25,
            Affiliation: affiliation,
            Confidence: 0.9,
            ChampionId: championId,
            IdentityConfidence: 0.85);
}
