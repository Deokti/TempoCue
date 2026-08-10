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
        var uncoveredLevels = new List<UncoveredLevel>();
        for (var level = MinimumGameLevel; level <= MaximumGameLevel; level++)
        {
            var levelPhases = validPhases
                .Where(phase => phase.AppliesToLevel(level))
                .ToArray();
            var isAmbiguous = HasCompatiblePair(levelPhases);
            var isUncovered = TryFindUncoveredItemState(
                levelPhases,
                out var itemStateWitness);

            if (isUncovered)
            {
                uncoveredLevels.Add(new UncoveredLevel(level, itemStateWitness));
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

    private static bool TryFindUncoveredItemState(
        IReadOnlyList<NormalizedPhase> phases,
        out MatchupItemStateWitness witness)
    {
        return TryFindUncoveredItemState(
            phases,
            new Dictionary<int, bool>(),
            out witness);
    }

    private static bool TryFindUncoveredItemState(
        IReadOnlyList<NormalizedPhase> phases,
        Dictionary<int, bool> assignment,
        out MatchupItemStateWitness witness)
    {
        var possiblePhases = new List<NormalizedPhase>(phases.Count);
        foreach (var phase in phases)
        {
            var evaluation = Evaluate(phase, assignment);
            if (evaluation == PhaseEvaluation.Satisfied)
            {
                witness = EmptyWitness();
                return false;
            }

            if (evaluation == PhaseEvaluation.Possible)
            {
                possiblePhases.Add(phase);
            }
        }

        if (possiblePhases.Count == 0)
        {
            witness = CreateWitness(assignment);
            return true;
        }

        var nextItemId = FindUnassignedItem(possiblePhases, assignment);
        assignment[nextItemId] = false;
        if (TryFindUncoveredItemState(possiblePhases, assignment, out witness))
        {
            assignment.Remove(nextItemId);
            return true;
        }

        assignment[nextItemId] = true;
        var hasUncoveredState = TryFindUncoveredItemState(
            possiblePhases,
            assignment,
            out witness);
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
        return phases
            .SelectMany(static phase => phase.RequiredItemIds.Concat(phase.ExcludedItemIds))
            .Where(itemId => !assignment.ContainsKey(itemId))
            .Min();
    }

    private static void AddUncoveredRangeIssues(
        IReadOnlyList<UncoveredLevel> uncoveredLevels,
        ICollection<MatchupPhaseValidationIssue> issues)
    {
        if (uncoveredLevels.Count == 0)
        {
            return;
        }

        var rangeStart = uncoveredLevels[0].Level;
        var rangeEnd = rangeStart;
        var witness = uncoveredLevels[0].Witness;
        for (var index = 1; index <= uncoveredLevels.Count; index++)
        {
            if (index < uncoveredLevels.Count &&
                uncoveredLevels[index].Level == rangeEnd + 1 &&
                WitnessesEqual(uncoveredLevels[index].Witness, witness))
            {
                rangeEnd = uncoveredLevels[index].Level;
                continue;
            }

            issues.Add(new MatchupPhaseValidationIssue(
                MatchupPhaseValidationIssueKind.UncoveredState,
                MatchupPhaseValidationSeverity.Warning,
                [],
                rangeStart,
                rangeEnd,
                witness));

            if (index < uncoveredLevels.Count)
            {
                rangeStart = uncoveredLevels[index].Level;
                rangeEnd = rangeStart;
                witness = uncoveredLevels[index].Witness;
            }
        }
    }

    private static MatchupItemStateWitness CreateWitness(
        IReadOnlyDictionary<int, bool> assignment) => new(
            assignment
                .Where(static pair => pair.Value)
                .Select(static pair => pair.Key)
                .Order()
                .ToArray(),
            assignment
                .Where(static pair => !pair.Value)
                .Select(static pair => pair.Key)
                .Order()
                .ToArray());

    private static MatchupItemStateWitness EmptyWitness() => new([], []);

    private static bool WitnessesEqual(
        MatchupItemStateWitness left,
        MatchupItemStateWitness right) =>
        left.PresentItemIds.SequenceEqual(right.PresentItemIds) &&
        left.AbsentItemIds.SequenceEqual(right.AbsentItemIds);

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

    private sealed record UncoveredLevel(
        int Level,
        MatchupItemStateWitness Witness);
}
