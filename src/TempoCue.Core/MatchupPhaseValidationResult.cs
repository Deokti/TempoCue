namespace TempoCue.Core;

/// <summary>Состояние покрытия одного игрового уровня фазами матчапа.</summary>
public enum MatchupLevelCoverageStatus
{
    Covered,
    Uncovered,
    Ambiguous
}

/// <summary>Результат проверки покрытия конкретного уровня.</summary>
public sealed record MatchupLevelCoverage(
    int Level,
    MatchupLevelCoverageStatus Status);

/// <summary>Полный результат детерминированной проверки набора фаз.</summary>
public sealed record MatchupPhaseValidationResult(
    IReadOnlyList<MatchupPhaseValidationIssue> Issues,
    IReadOnlyList<MatchupLevelCoverage> LevelCoverage)
{
    public bool HasErrors => Issues.Any(static issue =>
        issue.Severity == MatchupPhaseValidationSeverity.Error);

    public bool HasWarnings => Issues.Any(static issue =>
        issue.Severity == MatchupPhaseValidationSeverity.Warning);
}
