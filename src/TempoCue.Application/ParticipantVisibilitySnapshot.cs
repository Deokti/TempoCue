namespace TempoCue.Application;

/// <summary>Неизменяемый результат временного отслеживания одного участника.</summary>
public sealed record ParticipantVisibilitySnapshot(
    string ParticipantId,
    ParticipantVisibilityStatus Status,
    DateTimeOffset? LastSeenAt,
    double Confidence,
    double? MapX,
    double? MapY);
