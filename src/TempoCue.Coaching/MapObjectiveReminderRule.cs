namespace TempoCue.Coaching;

/// <summary>Пользовательское правило напоминания о нейтральном объекте.</summary>
public sealed record MapObjectiveReminderRule(
    string Id,
    MapObjectiveKind Objective,
    MapObjectiveReminderTrigger Trigger,
    TimeSpan LeadTime,
    MapObjectiveReminderRecurrence Recurrence,
    MapObjectiveReminderPriority Priority,
    string Message,
    string WhyNow,
    string NextGoal,
    bool IsEnabled = true);
