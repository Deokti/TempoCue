namespace TempoCue.Core;

/// <summary>Детерминированно находит фазу матчапа для выбранного уровня.</summary>
public sealed class MatchupPhaseSelector
{
    public MatchupPhaseSelectionResult Select(
        IReadOnlyCollection<MatchupPhaseCondition> phases,
        int level)
    {
        ArgumentNullException.ThrowIfNull(phases);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            level,
            MatchupPhaseSetValidator.MinimumGameLevel);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            level,
            MatchupPhaseSetValidator.MaximumGameLevel);

        var matchingPhaseIds = phases
            .Where(phase => AppliesToLevel(phase, level))
            .Select(static phase => phase.Id)
            .ToArray();
        return new MatchupPhaseSelectionResult(
            matchingPhaseIds.Length switch
            {
                0 => MatchupPhaseSelectionState.NotFound,
                1 => MatchupPhaseSelectionState.Selected,
                _ => MatchupPhaseSelectionState.Ambiguous
            },
            matchingPhaseIds);
    }

    private static bool AppliesToLevel(MatchupPhaseCondition phase, int level)
    {
        ArgumentNullException.ThrowIfNull(phase);
        return phase.MinimumLevel <= level
            && level <= (phase.MaximumLevel ?? MatchupPhaseSetValidator.MaximumGameLevel);
    }
}
