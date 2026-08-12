namespace TempoCue.Core;

/// <summary>Пользовательское правило напоминания о нейтральном объекте.</summary>
public sealed record MapObjectiveReminderRule(
    string Id,
    MapObjectiveKind Objective,
    MapObjectiveReminderTrigger Trigger,
    TimeSpan LeadTime,
    MapObjectiveReminderRecurrence Recurrence,
    MapObjectiveReminderPriority Priority,
    string Title,
    string Message,
    string WhyNow,
    string NextGoal);
