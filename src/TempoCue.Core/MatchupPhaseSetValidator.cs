namespace TempoCue.Core;

/// <summary>Проверяет, что для каждого игрового уровня подходит ровно одна фаза.</summary>
public sealed class MatchupPhaseSetValidator
{
    public const int MinimumGameLevel = 1;
    public const int MaximumGameLevel = 18;

    public MatchupPhaseValidationResult Validate(
        IReadOnlyCollection<MatchupPhaseCondition> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);

        var issues = new List<MatchupPhaseValidationIssue>();
        var validPhases = new List<NormalizedPhase>(phases.Count);
        foreach (var phase in phases)
        {
            ArgumentNullException.ThrowIfNull(phase);
            var normalized = Normalize(phase);
            if (!normalized.HasValidRange)
            {
                issues.Add(new MatchupPhaseValidationIssue(
                    MatchupPhaseValidationIssueKind.InvalidLevelRange,
                    MatchupPhaseValidationSeverity.Error,
                    [normalized.Id],
                    normalized.MinimumLevel,
                    normalized.MaximumLevel));
                continue;
            }

            validPhases.Add(normalized);
        }

        AddOverlapIssues(validPhases, issues);

        var coverage = new List<MatchupLevelCoverage>(MaximumGameLevel);
        var uncoveredLevels = new List<int>();
        for (var level = MinimumGameLevel; level <= MaximumGameLevel; level++)
        {
            var phaseCount = validPhases.Count(phase => phase.AppliesToLevel(level));
            if (phaseCount == 0)
            {
                uncoveredLevels.Add(level);
            }

            coverage.Add(new MatchupLevelCoverage(
                level,
                phaseCount switch
                {
                    0 => MatchupLevelCoverageStatus.Uncovered,
                    1 => MatchupLevelCoverageStatus.Covered,
                    _ => MatchupLevelCoverageStatus.Ambiguous
                }));
        }

        AddUncoveredRangeIssues(uncoveredLevels, issues);
        return new MatchupPhaseValidationResult(issues, coverage);
    }

    private static NormalizedPhase Normalize(MatchupPhaseCondition phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase.Id);
        return new NormalizedPhase(
            phase.Id,
            phase.MinimumLevel,
            phase.MaximumLevel ?? MaximumGameLevel);
    }

    private static void AddOverlapIssues(
        IReadOnlyList<NormalizedPhase> phases,
        ICollection<MatchupPhaseValidationIssue> issues)
    {
        for (var leftIndex = 0; leftIndex < phases.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < phases.Count; rightIndex++)
            {
                var left = phases[leftIndex];
                var right = phases[rightIndex];
                var minimumLevel = Math.Max(left.MinimumLevel, right.MinimumLevel);
                var maximumLevel = Math.Min(left.MaximumLevel, right.MaximumLevel);
                if (minimumLevel > maximumLevel)
                {
                    continue;
                }

                issues.Add(new MatchupPhaseValidationIssue(
                    MatchupPhaseValidationIssueKind.OverlappingPhases,
                    MatchupPhaseValidationSeverity.Error,
                    [left.Id, right.Id],
                    minimumLevel,
                    maximumLevel));
            }
        }
    }

    private static void AddUncoveredRangeIssues(
        IReadOnlyList<int> uncoveredLevels,
        ICollection<MatchupPhaseValidationIssue> issues)
    {
        if (uncoveredLevels.Count == 0)
        {
            return;
        }

        var rangeStart = uncoveredLevels[0];
        var rangeEnd = rangeStart;
        for (var index = 1; index <= uncoveredLevels.Count; index++)
        {
            if (index < uncoveredLevels.Count && uncoveredLevels[index] == rangeEnd + 1)
            {
                rangeEnd = uncoveredLevels[index];
                continue;
            }

            issues.Add(new MatchupPhaseValidationIssue(
                MatchupPhaseValidationIssueKind.UncoveredLevels,
                MatchupPhaseValidationSeverity.Warning,
                [],
                rangeStart,
                rangeEnd));

            if (index < uncoveredLevels.Count)
            {
                rangeStart = uncoveredLevels[index];
                rangeEnd = rangeStart;
            }
        }
    }

    private sealed record NormalizedPhase(
        string Id,
        int MinimumLevel,
        int MaximumLevel)
    {
        public bool HasValidRange =>
            MinimumLevel >= MinimumGameLevel &&
            MaximumLevel <= MaximumGameLevel &&
            MinimumLevel <= MaximumLevel;

        public bool AppliesToLevel(int level) =>
            level >= MinimumLevel && level <= MaximumLevel;
    }
}
