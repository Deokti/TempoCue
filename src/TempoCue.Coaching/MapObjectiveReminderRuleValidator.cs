namespace TempoCue.Coaching;

/// <summary>Проверяет правило напоминания относительно графика выбранного объекта.</summary>
public static class MapObjectiveReminderRuleValidator
{
    public static void Validate(MapObjectiveReminderRule rule, MapObjectiveTiming timing)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(timing);

        if (!Enum.IsDefined(rule.Objective)
            || !Enum.IsDefined(rule.Trigger)
            || !Enum.IsDefined(rule.Recurrence)
            || !Enum.IsDefined(rule.Priority))
        {
            throw new InvalidDataException($"Rule '{rule.Id}' contains an unsupported value.");
        }

        if (string.IsNullOrWhiteSpace(rule.Id)
            || string.IsNullOrWhiteSpace(rule.Message)
            || string.IsNullOrWhiteSpace(rule.WhyNow)
            || rule.NextGoal is null
            || rule.NextGoal.Length > 160)
        {
            throw new InvalidDataException(
                "Map objective reminder requires id, message and explanation.");
        }

        if (rule.Objective != timing.Objective)
        {
            throw new InvalidDataException(
                $"Rule '{rule.Id}' does not match timing for '{timing.Objective}'.");
        }

        if (rule.LeadTime < TimeSpan.Zero)
        {
            throw new InvalidDataException($"Rule '{rule.Id}' has a negative lead time.");
        }

        switch (rule.Trigger)
        {
            case MapObjectiveReminderTrigger.BeforeInitialSpawn:
                EnsurePositiveLeadTime(rule);
                if (rule.LeadTime > timing.InitialSpawnTime)
                {
                    throw new InvalidDataException(
                        $"Rule '{rule.Id}' is scheduled before the match starts.");
                }

                EnsureRepeatingInitialTriggerCanRespawn(rule, timing);
                if (rule.Recurrence == MapObjectiveReminderRecurrence.EveryCycle
                    && timing.RespawnDelay is { } initialReminderRespawnDelay
                    && rule.LeadTime > initialReminderRespawnDelay)
                {
                    throw new InvalidDataException(
                        $"Rule '{rule.Id}' is scheduled before the objective was killed.");
                }
                break;
            case MapObjectiveReminderTrigger.AtInitialSpawn:
                EnsureZeroLeadTime(rule);
                EnsureRepeatingInitialTriggerCanRespawn(rule, timing);
                break;
            case MapObjectiveReminderTrigger.BeforeRespawn:
                EnsurePositiveLeadTime(rule);
                if (timing.RespawnDelay is not { } respawnDelay)
                {
                    throw new InvalidDataException(
                        $"Objective '{rule.Objective}' does not respawn in this timing set.");
                }

                if (rule.LeadTime > respawnDelay)
                {
                    throw new InvalidDataException(
                        $"Rule '{rule.Id}' is scheduled before the objective was killed.");
                }

                break;
            case MapObjectiveReminderTrigger.AfterKill:
                EnsureZeroLeadTime(rule);
                break;
            default:
                throw new InvalidDataException(
                    $"Rule '{rule.Id}' has an unsupported trigger: {rule.Trigger}.");
        }
    }

    private static void EnsurePositiveLeadTime(MapObjectiveReminderRule rule)
    {
        if (rule.LeadTime <= TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"Rule '{rule.Id}' requires a positive lead time.");
        }
    }

    private static void EnsureZeroLeadTime(MapObjectiveReminderRule rule)
    {
        if (rule.LeadTime != TimeSpan.Zero)
        {
            throw new InvalidDataException(
                $"Rule '{rule.Id}' cannot use a lead time for trigger '{rule.Trigger}'.");
        }
    }

    private static void EnsureRepeatingInitialTriggerCanRespawn(
        MapObjectiveReminderRule rule,
        MapObjectiveTiming timing)
    {
        if (rule.Recurrence == MapObjectiveReminderRecurrence.EveryCycle
            && !timing.Respawns)
        {
            throw new InvalidDataException(
                $"Rule '{rule.Id}' cannot repeat because objective '{rule.Objective}' does not respawn.");
        }
    }
}
