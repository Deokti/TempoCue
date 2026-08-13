using TempoCue.Core;

namespace TempoCue.Coaching;

/// <summary>Строит стабильный план уведомлений одного проверенного Map Control Pack.</summary>
public sealed class MapControlPackPlanner
{
    private readonly MapControlPack _pack;
    private readonly IReadOnlyDictionary<MapObjectiveKind, MapObjectiveTiming> _timings;
    private readonly MapObjectiveReminderPlanner _objectivePlanner = new();

    public MapControlPackPlanner(MapControlPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        MapControlPackValidator.Validate(pack);

        if (!MapObjectiveTimingCatalog.TryGetForPatch(pack.Patch, out var timingSet)
            || timingSet is null)
        {
            throw new InvalidDataException(
                $"Map objective timings are unavailable for patch '{pack.Patch}'.");
        }

        _pack = pack;
        _timings = timingSet.Objectives.ToDictionary(static timing => timing.Objective);
    }

    public IReadOnlyList<MapControlScheduledAdvice> Build(
        IReadOnlyCollection<MapObjectiveKillEvent> killEvents)
    {
        ArgumentNullException.ThrowIfNull(killEvents);

        var scheduled = new List<MapControlScheduledAdvice>();
        foreach (var rule in _pack.Rules)
        {
            var timing = _timings[rule.Objective];
            scheduled.AddRange(_objectivePlanner
                .Build(rule, timing, killEvents)
                .Select(occurrence => FromOccurrence(_pack, rule, occurrence)));
        }

        scheduled.AddRange(_pack.TimedReminders.Select(reminder => new MapControlScheduledAdvice(
            $"{_pack.Id}:timed:{reminder.Id}",
            _pack.Id,
            _pack.Name,
            MapControlScheduledAdviceKind.TimedReminder,
            reminder.Priority,
            reminder.Title,
            reminder.Message,
            reminder.WhyNow,
            reminder.NextGoal,
            reminder.GameTime,
            reminder.GameTime)));

        return scheduled
            .OrderBy(static advice => advice.TriggerTime)
            .ThenByDescending(static advice => advice.Priority)
            .ThenBy(static advice => advice.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Возвращает постоянные подсказки, область которых применима в текущий момент матча.
    /// Порядок автора сохраняется, чтобы ротация оставалась детерминированной.
    /// </summary>
    public IReadOnlyList<MapControlPersistentAdvice> BuildPersistentAdvice(
        TimeSpan gameTime,
        IReadOnlyCollection<MapObjectiveKillEvent> killEvents)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gameTime, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(killEvents);

        var firstObjectiveTime = _timings.Values.Min(static timing => timing.InitialSpawnTime);
        return _pack.PersistentTips
            .Where(tip => IsApplicable(tip.Scope, gameTime, firstObjectiveTime, killEvents))
            .Select(tip => new MapControlPersistentAdvice(
                $"{_pack.Id}:persistent:{tip.Id}",
                _pack.Id,
                _pack.Name,
                tip.Title,
                tip.Message))
            .ToArray();
    }

    private bool IsApplicable(
        MapControlPersistentTipScope scope,
        TimeSpan gameTime,
        TimeSpan firstObjectiveTime,
        IReadOnlyCollection<MapObjectiveKillEvent> killEvents) => scope.Kind switch
        {
            MapControlPersistentTipScopeKind.WholeMatch => true,
            MapControlPersistentTipScopeKind.BeforeFirstObjective => gameTime < firstObjectiveTime,
            MapControlPersistentTipScopeKind.WhileAnyObjectiveAvailable =>
                _timings.Keys.Any(objective => IsObjectiveAvailable(objective, gameTime, killEvents)),
            MapControlPersistentTipScopeKind.SpecificObjective when scope.Objective is { } objective =>
                IsObjectiveAvailable(objective, gameTime, killEvents),
            _ => false
        };

    private bool IsObjectiveAvailable(
        MapObjectiveKind objective,
        TimeSpan gameTime,
        IReadOnlyCollection<MapObjectiveKillEvent> killEvents)
    {
        var timing = _timings[objective];
        if (gameTime < timing.InitialSpawnTime)
        {
            return false;
        }

        var latestKill = killEvents
            .Where(item => item.Objective == objective && item.GameTime <= gameTime)
            .OrderByDescending(static item => item.GameTime)
            .ThenByDescending(static item => item.EventId)
            .FirstOrDefault();
        if (latestKill is null)
        {
            return true;
        }

        return timing.RespawnDelay is { } respawnDelay
               && gameTime >= latestKill.GameTime + respawnDelay;
    }

    private static MapControlScheduledAdvice FromOccurrence(
        MapControlPack pack,
        MapObjectiveReminderRule rule,
        MapObjectiveReminderOccurrence occurrence) =>
        new(
            $"{pack.Id}:objective:{occurrence.Id}",
            pack.Id,
            pack.Name,
            MapControlScheduledAdviceKind.ObjectiveRule,
            rule.Priority,
            string.Empty,
            rule.Message,
            rule.WhyNow,
            rule.NextGoal,
            occurrence.TriggerTime,
            occurrence.TargetTime,
            occurrence.Objective);
}
