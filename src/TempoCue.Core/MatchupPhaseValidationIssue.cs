namespace TempoCue.Core;

/// <summary>Серьёзность проблемы в наборе фаз.</summary>
public enum MatchupPhaseValidationSeverity
{
    Warning,
    Error
}

/// <summary>Причина, по которой набор фаз требует внимания автора.</summary>
public enum MatchupPhaseValidationIssueKind
{
    InvalidLevelRange,
    ConflictingItemConditions,
    OverlappingPhases,
    UncoveredState
}

/// <summary>Пример состояния предметов, для которого не нашлось подходящей фазы.</summary>
public sealed record MatchupItemStateWitness(
    IReadOnlyList<int> PresentItemIds,
    IReadOnlyList<int> AbsentItemIds);

/// <summary>Проблема с указанием затронутых фаз и диапазона уровней.</summary>
public sealed record MatchupPhaseValidationIssue(
    MatchupPhaseValidationIssueKind Kind,
    MatchupPhaseValidationSeverity Severity,
    IReadOnlyList<string> PhaseIds,
    int? MinimumLevel = null,
    int? MaximumLevel = null,
    MatchupItemStateWitness? ItemStateWitness = null);
