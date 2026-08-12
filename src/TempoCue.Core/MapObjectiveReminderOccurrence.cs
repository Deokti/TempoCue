namespace TempoCue.Core;

/// <summary>Конкретный, воспроизводимый момент срабатывания правила в одном матче.</summary>
public sealed record MapObjectiveReminderOccurrence(
    string Id,
    string RuleId,
    MapObjectiveKind Objective,
    MapObjectiveReminderTrigger Trigger,
    TimeSpan TriggerTime,
    TimeSpan TargetTime,
    int? SourceEventId);
