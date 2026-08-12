namespace TempoCue.Coaching;

/// <summary>Детерминированно строит срабатывания одного правила для известных событий матча.</summary>
public sealed class MapObjectiveReminderPlanner
{
    public IReadOnlyList<MapObjectiveReminderOccurrence> Build(
        MapObjectiveReminderRule rule,
        MapObjectiveTiming timing,
        IReadOnlyCollection<MapObjectiveKillEvent> killEvents)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(timing);
        ArgumentNullException.ThrowIfNull(killEvents);
        MapObjectiveReminderRuleValidator.Validate(rule, timing);

        return rule.Trigger switch
        {
            MapObjectiveReminderTrigger.BeforeInitialSpawn =>
            [BuildInitialOccurrence(rule, timing, timing.InitialSpawnTime - rule.LeadTime)],
            MapObjectiveReminderTrigger.AtInitialSpawn =>
            [BuildInitialOccurrence(rule, timing, timing.InitialSpawnTime)],
            MapObjectiveReminderTrigger.BeforeRespawn =>
                BuildEventOccurrences(rule, timing, killEvents, beforeRespawn: true),
            MapObjectiveReminderTrigger.AfterKill =>
                BuildEventOccurrences(rule, timing, killEvents, beforeRespawn: false),
            _ => throw new InvalidDataException(
                $"Rule '{rule.Id}' has an unsupported trigger: {rule.Trigger}.")
        };
    }

    private static MapObjectiveReminderOccurrence BuildInitialOccurrence(
        MapObjectiveReminderRule rule,
        MapObjectiveTiming timing,
        TimeSpan triggerTime) =>
        new(
            $"{rule.Id}:initial",
            rule.Id,
            rule.Objective,
            rule.Trigger,
            triggerTime,
            timing.InitialSpawnTime,
            null);

    private static IReadOnlyList<MapObjectiveReminderOccurrence> BuildEventOccurrences(
        MapObjectiveReminderRule rule,
        MapObjectiveTiming timing,
        IReadOnlyCollection<MapObjectiveKillEvent> killEvents,
        bool beforeRespawn)
    {
        var matchingEvents = NormalizeEvents(rule, timing, killEvents);
        if (rule.Recurrence == MapObjectiveReminderRecurrence.OncePerMatch)
        {
            matchingEvents = matchingEvents.Take(1).ToArray();
        }

        return matchingEvents
            .Select(killEvent => BuildEventOccurrence(rule, timing, killEvent, beforeRespawn))
            .ToArray();
    }

    private static IReadOnlyList<MapObjectiveKillEvent> NormalizeEvents(
        MapObjectiveReminderRule rule,
        MapObjectiveTiming timing,
        IEnumerable<MapObjectiveKillEvent> killEvents)
    {
        var matchingEvents = killEvents
            .Where(killEvent => killEvent.Objective == rule.Objective)
            .OrderBy(static killEvent => killEvent.GameTime)
            .ThenBy(static killEvent => killEvent.EventId)
            .ToArray();

        if (matchingEvents.Any(static killEvent => killEvent.EventId < 0))
        {
            throw new InvalidDataException("Map objective event id cannot be negative.");
        }

        if (matchingEvents.Any(killEvent => killEvent.GameTime < timing.InitialSpawnTime))
        {
            throw new InvalidDataException(
                $"Objective '{rule.Objective}' cannot be killed before its initial spawn.");
        }

        var duplicateEventId = matchingEvents
            .GroupBy(static killEvent => killEvent.EventId)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateEventId is not null)
        {
            throw new InvalidDataException(
                $"Duplicate map objective event id: {duplicateEventId.Key}.");
        }

        return matchingEvents;
    }

    private static MapObjectiveReminderOccurrence BuildEventOccurrence(
        MapObjectiveReminderRule rule,
        MapObjectiveTiming timing,
        MapObjectiveKillEvent killEvent,
        bool beforeRespawn)
    {
        var respawnDelay = beforeRespawn
            ? timing.RespawnDelay
                ?? throw new InvalidOperationException("Validated respawn timing is missing.")
            : TimeSpan.Zero;
        var targetTime = beforeRespawn
            ? killEvent.GameTime + respawnDelay
            : killEvent.GameTime;
        var triggerTime = beforeRespawn
            ? targetTime - rule.LeadTime
            : killEvent.GameTime;

        return new MapObjectiveReminderOccurrence(
            $"{rule.Id}:event:{killEvent.EventId}",
            rule.Id,
            rule.Objective,
            rule.Trigger,
            triggerTime,
            targetTime,
            killEvent.EventId);
    }
}
