namespace TempoCue.Application;

/// <summary>
/// Нейтральное событие изменения видимости. Оно ещё не является советом или уведомлением.
/// </summary>
public sealed class ParticipantVisibilityTransition(
    string participantId,
    ParticipantVisibilityTransitionKind kind,
    DateTimeOffset occurredAt,
    DateTimeOffset? lastSeenAt,
    double confidence,
    double? mapX,
    double? mapY) : EventArgs
{
    public string ParticipantId { get; } = participantId;
    public ParticipantVisibilityTransitionKind Kind { get; } = kind;
    public DateTimeOffset OccurredAt { get; } = occurredAt;
    public DateTimeOffset? LastSeenAt { get; } = lastSeenAt;
    public double Confidence { get; } = confidence;
    public double? MapX { get; } = mapX;
    public double? MapY { get; } = mapY;
}
