using TempoCue.Core;

namespace TempoCue.Core.Tests;

public sealed class MatchupPhaseSetValidatorTests
{
    private readonly MatchupPhaseSetValidator _validator = new();

    [Fact]
    public void Validate_PartitionedByLevelAndItem_CoversEveryState()
    {
        var phases = new[]
        {
            Phase("early", 1, 8),
            Phase("before-cleaver", 9, null, excludedItems: [3071]),
            Phase("after-cleaver", 9, null, requiredItems: [3071])
        };

        var result = _validator.Validate(phases);

        Assert.Empty(result.Issues);
        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.All(result.LevelCoverage, coverage =>
            Assert.Equal(MatchupLevelCoverageStatus.Covered, coverage.Status));
    }

    [Fact]
    public void Validate_CompatiblePhasesAtSameLevels_ReportsOverlap()
    {
        var phases = new[]
        {
            Phase("first", 1, 8),
            Phase("second", 4, 10)
        };

        var result = _validator.Validate(phases);

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
    public void Validate_OppositeItemConditions_DoNotOverlap()
    {
        var phases = new[]
        {
            Phase("before", 1, null, excludedItems: [3071]),
            Phase("after", 1, null, requiredItems: [3071])
        };

        var result = _validator.Validate(phases);

        Assert.Empty(result.Issues);
        Assert.All(result.LevelCoverage, coverage =>
            Assert.Equal(MatchupLevelCoverageStatus.Covered, coverage.Status));
    }

    [Fact]
    public void Validate_InvalidRangeAndSameItemOnBothSides_ReportErrors()
    {
        var phases = new[]
        {
            Phase("invalid-range", 9, 8),
            Phase("conflicting-item", 1, null, [3071], [3071])
        };

        var result = _validator.Validate(phases);

        Assert.Contains(result.Issues, issue =>
            issue.Kind == MatchupPhaseValidationIssueKind.InvalidLevelRange &&
            issue.PhaseIds.SequenceEqual(["invalid-range"]));
        Assert.Contains(result.Issues, issue =>
            issue.Kind == MatchupPhaseValidationIssueKind.ConflictingItemConditions &&
            issue.PhaseIds.SequenceEqual(["conflicting-item"]));
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void Validate_MissingLevelRanges_GroupsAdjacentWarnings()
    {
        var phases = new[]
        {
            Phase("middle", 4, 8),
            Phase("late", 10, null)
        };

        var result = _validator.Validate(phases);

        var gaps = result.Issues
            .Where(issue => issue.Kind == MatchupPhaseValidationIssueKind.UncoveredState)
            .ToArray();
        Assert.Collection(
            gaps,
            gap => Assert.Equal((1, 3), (gap.MinimumLevel, gap.MaximumLevel)),
            gap => Assert.Equal((9, 9), (gap.MinimumLevel, gap.MaximumLevel)));
        Assert.True(result.HasWarnings);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Validate_OnlyRequiredItemPhase_ReportsMissingItemState()
    {
        var result = _validator.Validate([
            Phase("after", 1, null, requiredItems: [3071, 3047])
        ]);

        var gap = Assert.Single(result.Issues, issue =>
            issue.Kind == MatchupPhaseValidationIssueKind.UncoveredState);
        Assert.Equal((1, 18), (gap.MinimumLevel, gap.MaximumLevel));
        Assert.Equal([3047], gap.ItemStateWitness?.AbsentItemIds);
        Assert.Empty(gap.ItemStateWitness?.PresentItemIds ?? []);
        Assert.All(result.LevelCoverage, coverage =>
            Assert.Equal(MatchupLevelCoverageStatus.Uncovered, coverage.Status));
    }

    [Fact]
    public void Validate_DifferentRequiredItems_SplitsGapByActionableWitness()
    {
        var result = _validator.Validate([
            Phase("early", 1, 8, requiredItems: [2003, 1055]),
            Phase("late", 9, null, requiredItems: [3071])
        ]);

        var gaps = result.Issues
            .Where(issue => issue.Kind == MatchupPhaseValidationIssueKind.UncoveredState)
            .ToArray();
        Assert.Collection(
            gaps,
            gap =>
            {
                Assert.Equal((1, 8), (gap.MinimumLevel, gap.MaximumLevel));
                Assert.Equal([1055], gap.ItemStateWitness?.AbsentItemIds);
            },
            gap =>
            {
                Assert.Equal((9, 18), (gap.MinimumLevel, gap.MaximumLevel));
                Assert.Equal([3071], gap.ItemStateWitness?.AbsentItemIds);
            });
    }

    private static MatchupPhaseCondition Phase(
        string id,
        int minimumLevel,
        int? maximumLevel,
        IReadOnlyCollection<int>? requiredItems = null,
        IReadOnlyCollection<int>? excludedItems = null) => new(
            id,
            minimumLevel,
            maximumLevel,
            requiredItems ?? [],
            excludedItems ?? []);
}
