namespace TempoCue.Coaching;

/// <summary>Одноразовое напоминание, привязанное к точному игровому времени.</summary>
public sealed record MapControlTimedReminder
{
    public required string Id { get; init; }

    public TimeSpan GameTime { get; init; }

    public MapObjectiveReminderPriority Priority { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public required string WhyNow { get; init; }

    public required string NextGoal { get; init; }
}
