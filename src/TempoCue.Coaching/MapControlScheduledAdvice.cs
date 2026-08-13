namespace TempoCue.Coaching;

/// <summary>
/// Готовый к показу совет контроля карты с воспроизводимым временем срабатывания.
/// Представление само решает, как отобразить его, и не интерпретирует правила набора.
/// </summary>
public sealed record MapControlScheduledAdvice(
    string Id,
    string PackId,
    string PackName,
    MapControlScheduledAdviceKind Kind,
    MapObjectiveReminderPriority Priority,
    string Title,
    string Message,
    string WhyNow,
    string NextGoal,
    TimeSpan TriggerTime,
    TimeSpan TargetTime,
    MapObjectiveKind? Objective = null);
