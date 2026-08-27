using System.IO;
using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>
/// Загружает включённые Map Control Pack, применяет ограничения роли и стороны,
/// а затем детерминированно объединяет их расписания без одинаковых уведомлений.
/// </summary>
public sealed class SelectActiveMapControlPacksUseCase : IMapControlPlanSelector
{
    private readonly LoadMapControlPackLibraryUseCase _loadLibrary;
    private readonly IReadOnlyList<MapControlPack> _builtInPacks;
    private readonly HashSet<string> _builtInPackIds;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly object _planCacheLock = new();

    private LibraryLoadResult? _cachedLibraryLoad;
    private PlanCacheEntry? _cachedPlan;
    private long _cacheVersion;
    private long _loadedVersion = -1;

    public SelectActiveMapControlPacksUseCase(
        LoadMapControlPackLibraryUseCase loadLibrary,
        IReadOnlyList<MapControlPack>? builtInPacks = null)
    {
        _loadLibrary = loadLibrary ?? throw new ArgumentNullException(nameof(loadLibrary));
        _builtInPacks = (builtInPacks ?? []).ToArray();
        _builtInPackIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pack in _builtInPacks)
        {
            MapControlPackValidator.Validate(pack);
            if (!_builtInPackIds.Add(pack.Id))
            {
                throw new InvalidDataException(
                    $"Duplicate built-in Map Control Pack id: {pack.Id}.");
            }
        }
    }

    public async ValueTask<ActiveMapControlPlan> SelectAsync(
        MatchSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var activePlayer = snapshot.Match.GetActivePlayer();
        if (activePlayer is null)
        {
            return ActiveMapControlPlan.Empty;
        }

        var libraryLoad = await GetLibraryAsync(cancellationToken);
        if (libraryLoad.Packs is null)
        {
            return ActiveMapControlPlan.Empty;
        }

        var killEvents = ToObjectiveKillEvents(snapshot.Events);
        var activePacks = libraryLoad.Packs
            .Where(pack => AppliesTo(pack.Pack.Applicability, activePlayer))
            .OrderBy(pack => pack.Pack.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pack => pack.Pack.Id, StringComparer.Ordinal)
            .ToArray();
        if (activePacks.Length == 0)
        {
            return CachePlan(
                libraryLoad.Version,
                activePlayer.Position,
                activePlayer.Team,
                killEvents,
                ActiveMapControlPlan.Empty);
        }

        var gameTime = TimeSpan.FromSeconds(Math.Max(0, snapshot.Match.GameTimeSeconds));
        var persistentAdvice = activePacks
            .SelectMany(pack => pack.Planner.BuildPersistentAdvice(gameTime, killEvents))
            .GroupBy(static item => new PersistentAdviceDeduplicationKey(
                item.Title.Trim(),
                item.Message.Trim()))
            .Select(static group => group.First())
            .ToArray();
        var persistentAdviceIds = persistentAdvice
            .Select(static item => item.Id)
            .ToArray();
        if (TryGetCachedPlan(
                libraryLoad.Version,
                activePlayer.Position,
                activePlayer.Team,
                killEvents,
                persistentAdviceIds,
                out var cachedPlan))
        {
            return cachedPlan;
        }

        var advice = activePacks
            .SelectMany(pack => pack.Planner.Build(killEvents))
            .OrderBy(static item => item.TriggerTime)
            .ThenByDescending(static item => item.Priority)
            .ThenBy(static item => item.PackName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .GroupBy(static item => new AdviceDeduplicationKey(
                item.Kind,
                item.Objective,
                item.TriggerTime,
                item.Title.Trim(),
                item.Message.Trim()))
            .Select(static group => group.First())
            .ToArray();

        return CachePlan(
            libraryLoad.Version,
            activePlayer.Position,
            activePlayer.Team,
            killEvents,
            new ActiveMapControlPlan(
                advice,
                activePacks.Select(static pack => pack.Pack.Name).ToArray(),
                persistentAdvice,
                CollectVisionRules(activePacks)));
    }

    /// <summary>
    /// Включённые правила наблюдения всех подходящих наборов в авторском порядке.
    /// Выключенные отбрасываются здесь, а не при сопоставлении: дальше по цепочке
    /// правило уже считается разрешённым.
    /// </summary>
    private static IReadOnlyList<MapVisionRule> CollectVisionRules(
        IEnumerable<PlannedPack> packs) =>
        packs
            .SelectMany(static pack => pack.Pack.VisionRules)
            .Where(static rule => rule.IsEnabled)
            .ToArray();

    /// <summary>Помечает кеш устаревшим после изменения библиотеки.</summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _cacheVersion);
        lock (_planCacheLock)
        {
            _cachedPlan = null;
        }
    }

    private async Task<LibraryLoadResult> GetLibraryAsync(CancellationToken cancellationToken)
    {
        var requestedVersion = Volatile.Read(ref _cacheVersion);
        if (_cachedLibraryLoad is not null
            && Volatile.Read(ref _loadedVersion) == requestedVersion)
        {
            return _cachedLibraryLoad;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            requestedVersion = Volatile.Read(ref _cacheVersion);
            if (_cachedLibraryLoad is not null
                && Volatile.Read(ref _loadedVersion) == requestedVersion)
            {
                return _cachedLibraryLoad;
            }

            LibraryLoadResult result;
            try
            {
                var snapshot = await _loadLibrary.ExecuteAsync(cancellationToken);
                var enabledBuiltInPacks = _builtInPacks
                    .Where(pack => snapshot.PackStates.GetValueOrDefault(
                        pack.Id,
                        LocalCoachPackState.Enabled) == LocalCoachPackState.Enabled)
                    .Select(static pack => new PlannedPack(
                        pack,
                        new MapControlPackPlanner(pack)));
                result = new LibraryLoadResult(
                    enabledBuiltInPacks.Concat(snapshot.Entries
                        .Where(entry => entry.State == LocalCoachPackState.Enabled
                                        && !_builtInPackIds.Contains(entry.Pack.Id))
                        .Select(static entry => new PlannedPack(
                            entry.Pack,
                            new MapControlPackPlanner(entry.Pack))))
                        .ToArray(),
                    requestedVersion);
            }
            catch (Exception exception) when (IsExpectedLibraryException(exception))
            {
                result = new LibraryLoadResult(null, requestedVersion);
            }

            _cachedLibraryLoad = result;
            Volatile.Write(ref _loadedVersion, requestedVersion);
            return result;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static bool AppliesTo(MapControlApplicability applicability, PlayerState player)
    {
        var role = player.Position switch
        {
            PlayerPosition.Top => MapControlRole.Top,
            PlayerPosition.Jungle => MapControlRole.Jungle,
            PlayerPosition.Middle => MapControlRole.Mid,
            PlayerPosition.Bottom => MapControlRole.Adc,
            PlayerPosition.Support => MapControlRole.Support,
            _ => (MapControlRole?)null
        };
        var appliesToRole = role is not null
            ? applicability.Roles.Contains(role.Value)
            : applicability.Roles.Count == MapControlRoleValues.All.Count
              && MapControlRoleValues.All.All(applicability.Roles.Contains);
        if (!appliesToRole)
        {
            return false;
        }

        var side = player.Team switch
        {
            TeamSide.Order => MapControlTeamSide.Blue,
            TeamSide.Chaos => MapControlTeamSide.Red,
            _ => (MapControlTeamSide?)null
        };
        return applicability.TeamSide == MapControlTeamSide.Any
               || side is not null && applicability.TeamSide == side.Value;
    }

    private static IReadOnlyList<MapObjectiveKillEvent> ToObjectiveKillEvents(
        IEnumerable<MatchEvent> events) =>
        events.Select(ToObjectiveKillEvent)
            .Where(static item => item is not null)
            .Cast<MapObjectiveKillEvent>()
            .OrderBy(static item => item.GameTime)
            .ThenBy(static item => item.EventId)
            .ToArray();

    private static MapObjectiveKillEvent? ToObjectiveKillEvent(MatchEvent matchEvent)
    {
        var objective = matchEvent.Kind switch
        {
            MatchEventKind.DragonKill when matchEvent.DragonType != DragonType.Elder =>
                MapObjectiveKind.ElementalDragon,
            MatchEventKind.RiftHeraldKill => MapObjectiveKind.RiftHerald,
            MatchEventKind.BaronNashorKill => MapObjectiveKind.BaronNashor,
            _ => (MapObjectiveKind?)null
        };
        return objective is null
            ? null
            : new MapObjectiveKillEvent(
                matchEvent.EventId,
                objective.Value,
                TimeSpan.FromSeconds(matchEvent.GameTimeSeconds));
    }

    private static bool IsExpectedLibraryException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException;

    private bool TryGetCachedPlan(
        long libraryVersion,
        PlayerPosition position,
        TeamSide team,
        IReadOnlyList<MapObjectiveKillEvent> killEvents,
        IReadOnlyList<string> persistentAdviceIds,
        out ActiveMapControlPlan plan)
    {
        lock (_planCacheLock)
        {
            if (_cachedPlan is { } cached
                && cached.LibraryVersion == libraryVersion
                && cached.Position == position
                && cached.Team == team
                && cached.KillEvents.SequenceEqual(killEvents)
                && cached.PersistentAdviceIds.SequenceEqual(persistentAdviceIds))
            {
                plan = cached.Plan;
                return true;
            }
        }

        plan = ActiveMapControlPlan.Empty;
        return false;
    }

    private ActiveMapControlPlan CachePlan(
        long libraryVersion,
        PlayerPosition position,
        TeamSide team,
        IReadOnlyList<MapObjectiveKillEvent> killEvents,
        ActiveMapControlPlan plan)
    {
        lock (_planCacheLock)
        {
            _cachedPlan = new PlanCacheEntry(
                libraryVersion,
                position,
                team,
                killEvents.ToArray(),
                plan.AvailablePersistentAdvice.Select(static item => item.Id).ToArray(),
                plan);
        }

        return plan;
    }

    private sealed record PlannedPack(MapControlPack Pack, MapControlPackPlanner Planner);

    private sealed record LibraryLoadResult(
        IReadOnlyList<PlannedPack>? Packs,
        long Version);

    private sealed record PlanCacheEntry(
        long LibraryVersion,
        PlayerPosition Position,
        TeamSide Team,
        IReadOnlyList<MapObjectiveKillEvent> KillEvents,
        IReadOnlyList<string> PersistentAdviceIds,
        ActiveMapControlPlan Plan);

    private sealed record AdviceDeduplicationKey(
        MapControlScheduledAdviceKind Kind,
        MapObjectiveKind? Objective,
        TimeSpan TriggerTime,
        string Title,
        string Message);

    private sealed record PersistentAdviceDeduplicationKey(
        string Title,
        string Message);
}
