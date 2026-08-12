namespace TempoCue.Coaching;

/// <summary>Стабильный результат выбора фазы без зависимости от интерфейса.</summary>
public sealed record MatchupPhaseSelectionResult(
    MatchupPhaseSelectionState State,
    IReadOnlyList<string> MatchingPhaseIds)
{
    public string? SelectedPhaseId => State == MatchupPhaseSelectionState.Selected
        ? MatchingPhaseIds[0]
        : null;
}
