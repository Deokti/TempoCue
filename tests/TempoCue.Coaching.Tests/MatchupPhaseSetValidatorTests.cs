using TempoCue.Core;

namespace TempoCue.Coaching.Tests;

public sealed class MatchupPhaseSetValidatorTests
{
    private readonly MatchupPhaseSetValidator _validator = new();

    [Fact]
    public void Validate_PartitionedByLevel_CoversEveryLevel()
    {
        var result = _validator.Validate([
            Phase("early", 1, 8),
            Phase("late", 9, null)
        ]);

        Assert.Empty(result.Issues);
        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.All(result.LevelCoverage, coverage =>
            Assert.Equal(MatchupLevelCoverageStatus.Covered, coverage.Status));
    }

    [Fact]
    public void Validate_OverlappingLevelRanges_ReportConflict()
    {
        var result = _validator.Validate([
            Phase("first", 1, 8),
            Phase("second", 4, 10)
        ]);

        var overlap = Assert.Single(result.Issues, issue =>
            issue.Kind == MatchupPhaseValidationIssueKind.OverlappingPhases);
        Assert.Equal(MatchupPhaseValidationSeverity.Error, overlap.Severity);
        Assert.Equal(["first", "second"], overlap.PhaseIds);
        Assert.Equal(4, overlap.MinimumLevel);
        Assert.Equal(8, overlap.MaximumLevel);
        Assert.True(result.HasErrors);
        Assert.Equal(
            MatchupLevelCoverageStatus.Ambiguous,
            result.LevelCoverage.Single(coverage => coverage.Level == 4).Status);
    }

    [Fact]
    public void Validate_InvalidRange_ReportsError()
    {
        var result = _validator.Validate([Phase("invalid-range", 9, 8)]);

        var issue = Assert.Single(result.Issues, issue =>
            issue.Kind == MatchupPhaseValidationIssueKind.InvalidLevelRange);
        Assert.Equal(["invalid-range"], issue.PhaseIds);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Validate_MissingLevelRanges_GroupsAdjacentWarnings()
    {
        var result = _validator.Validate([
            Phase("middle", 4, 8),
            Phase("late", 10, null)
        ]);

        var gaps = result.Issues
            .Where(issue => issue.Kind == MatchupPhaseValidationIssueKind.UncoveredLevels)
            .ToArray();
        Assert.Collection(
            gaps,
            gap => Assert.Equal((1, 3), (gap.MinimumLevel, gap.MaximumLevel)),
            gap => Assert.Equal((9, 9), (gap.MinimumLevel, gap.MaximumLevel)));
        Assert.True(result.HasWarnings);
        Assert.False(result.HasErrors);
    }

    private static MatchupPhaseCondition Phase(
        string id,
        int minimumLevel,
        int? maximumLevel) => new(id, minimumLevel, maximumLevel);
}
