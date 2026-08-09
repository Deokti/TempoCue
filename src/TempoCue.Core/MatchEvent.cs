namespace TempoCue.Core;

/// <summary>
/// Нормализованный факт активного матча. Имена полей выражают игровой смысл
/// и не зависят от названий свойств Riot JSON.
/// </summary>
public sealed record MatchEvent
{
    public required int EventId { get; init; }

    public required MatchEventKind Kind { get; init; }

    public required double GameTimeSeconds { get; init; }

    public string? KillerParticipantId { get; init; }

    public string? VictimParticipantId { get; init; }

    public IReadOnlyList<string> AssisterParticipantIds { get; init; } = [];

    public DragonType DragonType { get; init; } = DragonType.Unknown;
}
