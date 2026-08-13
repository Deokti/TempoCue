namespace TempoCue.Coaching;

/// <summary>Проверяет напоминание по времени на границе переносимого набора.</summary>
public static class MapControlTimedReminderValidator
{
    public static readonly TimeSpan MaximumGameTime = TimeSpan.FromMinutes(90);

    public static void Validate(MapControlTimedReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        if (string.IsNullOrWhiteSpace(reminder.Id)
            || reminder.Title.Trim().Length is < 3 or > 80
            || reminder.Message.Trim().Length is < 3 or > 240
            || reminder.WhyNow.Trim().Length is < 3 or > 240
            || reminder.NextGoal is null
            || reminder.NextGoal.Trim().Length > 240)
        {
            throw new InvalidDataException(
                "Map Control timed reminder requires a valid id and advice text.");
        }

        if (reminder.GameTime < TimeSpan.Zero
            || reminder.GameTime > MaximumGameTime
            || reminder.GameTime.Ticks % TimeSpan.FromSeconds(15).Ticks != 0)
        {
            throw new InvalidDataException(
                $"Timed reminder '{reminder.Id}' must use a 15-second game-time step between 00:00 and 90:00.");
        }

        if (!Enum.IsDefined(reminder.Priority))
        {
            throw new InvalidDataException(
                $"Timed reminder '{reminder.Id}' has an unsupported priority.");
        }
    }
}
