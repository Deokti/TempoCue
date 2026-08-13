namespace TempoCue.Coaching;

/// <summary>
/// Условия применимости одной фазы матчапа, не зависящие от её отображения и текста совета.
/// </summary>
public sealed record MatchupPhaseCondition(
    string Id,
    int MinimumLevel,
    int? MaximumLevel);
