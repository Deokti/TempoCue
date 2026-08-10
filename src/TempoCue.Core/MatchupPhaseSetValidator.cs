namespace TempoCue.Core;

/// <summary>
/// Проверяет, что для каждого уровня и набора учитываемых предметов подходит ровно одна фаза.
/// </summary>
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
            var isValid = true;

            if (normalized.MinimumLevel < MinimumGameLevel ||
                normalized.MaximumLevel > MaximumGameLevel ||
                normalized.MinimumLevel > normalized.MaximumLevel)
            {
                issues.Add(new MatchupPhaseValidationIssue(
                    MatchupPhaseValidationIssueKind.InvalidLevelRange,
                    MatchupPhaseValidationSeverity.Error,
                    [normalized.Id],
                    normalized.MinimumLevel,
                    normalized.MaximumLevel));
                isValid = false;
            }

            if (normalized.RequiredItemIds.Overlaps(normalized.ExcludedItemIds))
            {
                issues.Add(new MatchupPhaseValidationIssue(
                    MatchupPhaseValidationIssueKind.ConflictingItemConditions,
                    MatchupPhaseValidationSeverity.Error,
                    [normalized.Id],
                    normalized.MinimumLevel,
                    normalized.MaximumLevel));
                isValid = false;
            }

            if (isValid)
            {
                validPhases.Add(normalized);
            }
        }

        AddOverlapIssues(validPhases, issues);

        var coverage = new List<MatchupLevelCoverage>(MaximumGameLevel);
        var uncoveredLevels = new List<int>();
        for (var level = MinimumGameLevel; level <= MaximumGameLevel; level++)
        {
            var levelPhases = validPhases
                .Where(phase => phase.AppliesToLevel(level))
                .ToArray();
            var isAmbiguous = HasCompatiblePair(levelPhases);
            var isUncovered = HasUncoveredItemState(levelPhases);

            if (isUncovered)
            {
                uncoveredLevels.Add(level);
            }

            coverage.Add(new MatchupLevelCoverage(
                level,
                isAmbiguous
                    ? MatchupLevelCoverageStatus.Ambiguous
                    : isUncovered
                        ? MatchupLevelCoverageStatus.Uncovered
                        : MatchupLevelCoverageStatus.Covered));
        }

        AddUncoveredRangeIssues(uncoveredLevels, issues);
        return new MatchupPhaseValidationResult(issues, coverage);
    }

    private static NormalizedPhase Normalize(MatchupPhaseCondition phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase.Id);
        ArgumentNullException.ThrowIfNull(phase.RequiredItemIds);
        ArgumentNullException.ThrowIfNull(phase.ExcludedItemIds);

        return new NormalizedPhase(
            phase.Id,
            phase.MinimumLevel,
            phase.MaximumLevel ?? MaximumGameLevel,
            new HashSet<int>(phase.RequiredItemIds),
            new HashSet<int>(phase.ExcludedItemIds));
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
                if (minimumLevel > maximumLevel || AreMutuallyExclusive(left, right))
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

    private static bool HasCompatiblePair(IReadOnlyList<NormalizedPhase> phases)
    {
        for (var leftIndex = 0; leftIndex < phases.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < phases.Count; rightIndex++)
            {
                if (!AreMutuallyExclusive(phases[leftIndex], phases[rightIndex]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreMutuallyExclusive(NormalizedPhase left, NormalizedPhase right) =>
        left.RequiredItemIds.Overlaps(right.ExcludedItemIds) ||
        right.RequiredItemIds.Overlaps(left.ExcludedItemIds);

    private static bool HasUncoveredItemState(IReadOnlyList<NormalizedPhase> phases)
    {
        if (phases.Count == 0)
        {
            return true;
        }

        return HasUncoveredItemState(phases, new Dictionary<int, bool>());
    }

    private static bool HasUncoveredItemState(
        IReadOnlyList<NormalizedPhase> phases,
        Dictionary<int, bool> assignment)
    {
        var possiblePhases = new List<NormalizedPhase>(phases.Count);
        foreach (var phase in phases)
        {
            var evaluation = Evaluate(phase, assignment);
            if (evaluation == PhaseEvaluation.Satisfied)
            {
                return false;
            }

            if (evaluation == PhaseEvaluation.Possible)
            {
                possiblePhases.Add(phase);
            }
        }

        if (possiblePhases.Count == 0)
        {
            return true;
        }

        var nextItemId = FindUnassignedItem(possiblePhases, assignment);
        assignment[nextItemId] = false;
        if (HasUncoveredItemState(possiblePhases, assignment))
        {
            assignment.Remove(nextItemId);
            return true;
        }

        assignment[nextItemId] = true;
        var hasUncoveredState = HasUncoveredItemState(possiblePhases, assignment);
        assignment.Remove(nextItemId);
        return hasUncoveredState;
    }

    private static PhaseEvaluation Evaluate(
        NormalizedPhase phase,
        IReadOnlyDictionary<int, bool> assignment)
    {
        var hasUnassignedCondition = false;
        foreach (var itemId in phase.RequiredItemIds)
        {
            if (!assignment.TryGetValue(itemId, out var isPresent))
            {
                hasUnassignedCondition = true;
            }
            else if (!isPresent)
            {
                return PhaseEvaluation.Contradicted;
            }
        }

        foreach (var itemId in phase.ExcludedItemIds)
        {
            if (!assignment.TryGetValue(itemId, out var isPresent))
            {
                hasUnassignedCondition = true;
            }
            else if (isPresent)
            {
                return PhaseEvaluation.Contradicted;
            }
        }

        return hasUnassignedCondition
            ? PhaseEvaluation.Possible
            : PhaseEvaluation.Satisfied;
    }

    private static int FindUnassignedItem(
        IEnumerable<NormalizedPhase> phases,
        IReadOnlyDictionary<int, bool> assignment)
    {
        foreach (var phase in phases)
        {
            foreach (var itemId in phase.RequiredItemIds.Concat(phase.ExcludedItemIds))
            {
                if (!assignment.ContainsKey(itemId))
                {
                    return itemId;
                }
            }
        }

        throw new InvalidOperationException("Для возможной фазы не найдено непроверенное условие предмета.");
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
                MatchupPhaseValidationIssueKind.UncoveredState,
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

    private enum PhaseEvaluation
    {
        Possible,
        Satisfied,
        Contradicted
    }

    private sealed record NormalizedPhase(
        string Id,
        int MinimumLevel,
        int MaximumLevel,
        HashSet<int> RequiredItemIds,
        HashSet<int> ExcludedItemIds)
    {
        public bool AppliesToLevel(int level) =>
            level >= MinimumLevel && level <= MaximumLevel;
    }
}
