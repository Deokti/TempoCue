using System.IO;
using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>
/// Детерминированно выбирает включённый пользовательский матчап,
/// а при его отсутствии проверяет встроенные наборы.
/// </summary>
public sealed class SelectActiveCoachPackUseCase : IMatchAdviceSelector
{
    private readonly IReadOnlyList<CoachPack> _builtInPacks;
    private readonly LoadCoachPackLibraryUseCase _loadLibrary;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private LibraryLoadResult? _cachedLibraryLoad;
    private long _cacheVersion;
    private long _loadedVersion = -1;

    public SelectActiveCoachPackUseCase(
        IReadOnlyList<CoachPack> builtInPacks,
        LoadCoachPackLibraryUseCase loadLibrary)
    {
        ArgumentNullException.ThrowIfNull(builtInPacks);
        _builtInPacks = builtInPacks.ToArray();
        _loadLibrary = loadLibrary ?? throw new ArgumentNullException(nameof(loadLibrary));
    }

    public async ValueTask<ActiveCoachPackSelection> SelectAsync(
        MatchState match,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);

        var matchup = MatchupResolver.Resolve(match);
        if (matchup is null)
        {
            return ActiveCoachPackSelection.MatchupUnavailable();
        }

        var libraryLoad = await GetLibraryAsync(cancellationToken);
        if (libraryLoad.Snapshot is not { } library)
        {
            return ActiveCoachPackSelection.LibraryUnavailable();
        }

        var matchingUserPacks = library.Entries
            .Where(static entry => entry.State == LocalCoachPackState.Enabled)
            .Select(static entry => entry.Pack)
            .Where(pack => AppliesToMatchup(pack, matchup))
            .OrderBy(static pack => pack.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static pack => pack.Id, StringComparer.Ordinal)
            .ToArray();

        if (matchingUserPacks.Length > 1)
        {
            return ActiveCoachPackSelection.Conflict(
                matchingUserPacks.Select(static pack => pack.Name).ToArray());
        }

        if (matchingUserPacks.Length == 1)
        {
            return Evaluate(matchingUserPacks[0], matchup);
        }

        var matchingBuiltInPacks = _builtInPacks
            .Where(pack => library.PackStates.GetValueOrDefault(
                pack.Id,
                LocalCoachPackState.Enabled) == LocalCoachPackState.Enabled)
            .Where(pack => AppliesToMatchup(pack, matchup))
            .OrderBy(static pack => pack.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static pack => pack.Id, StringComparer.Ordinal)
            .ToArray();

        return matchingBuiltInPacks.Length switch
        {
            0 => ActiveCoachPackSelection.NotFound(),
            1 => Evaluate(matchingBuiltInPacks[0], matchup),
            _ => ActiveCoachPackSelection.Conflict(
                matchingBuiltInPacks.Select(static pack => pack.Name).ToArray())
        };
    }

    /// <summary>
    /// Помечает кеш устаревшим после сохранения набора или изменения его состояния.
    /// Следующий снимок матча перечитает библиотеку ровно один раз.
    /// </summary>
    public void Invalidate() => Interlocked.Increment(ref _cacheVersion);

    private async Task<LibraryLoadResult> GetLibraryAsync(
        CancellationToken cancellationToken)
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

            LibraryLoadResult loadResult;
            try
            {
                loadResult = new LibraryLoadResult(
                    await _loadLibrary.ExecuteAsync(cancellationToken));
            }
            catch (Exception exception) when (IsExpectedLibraryException(exception))
            {
                loadResult = new LibraryLoadResult(null);
            }

            _cachedLibraryLoad = loadResult;
            Volatile.Write(ref _loadedVersion, requestedVersion);
            return loadResult;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static ActiveCoachPackSelection Evaluate(
        CoachPack pack,
        MatchupContext matchup)
    {
        var advice = new MatchupCoach(pack.Rules).GetBestAdvice(matchup);
        return ActiveCoachPackSelection.Selected(pack, advice);
    }

    private static bool AppliesToMatchup(CoachPack pack, MatchupContext matchup)
    {
        var firstRule = pack.Rules.FirstOrDefault();
        var playerChampion = FirstNotEmpty(pack.PlayerChampion, firstRule?.PlayerChampion);
        var opponentChampion = FirstNotEmpty(pack.OpponentChampion, firstRule?.OpponentChampion);

        return string.Equals(
                   playerChampion,
                   matchup.Player.ChampionName,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   opponentChampion,
                   matchup.Opponent.ChampionName,
                   StringComparison.OrdinalIgnoreCase)
               && AppliesToPosition(pack.LaneRole, matchup.Player.Position);
    }

    private static bool AppliesToPosition(string laneRole, PlayerPosition position)
    {
        if (string.IsNullOrWhiteSpace(laneRole)
            || laneRole.Equals("any", StringComparison.OrdinalIgnoreCase)
            || position == PlayerPosition.Unknown)
        {
            return true;
        }

        var expectedRole = position switch
        {
            PlayerPosition.Top => "top",
            PlayerPosition.Jungle => "jungle",
            PlayerPosition.Middle => "mid",
            PlayerPosition.Bottom => "adc",
            PlayerPosition.Support => "support",
            _ => string.Empty
        };
        return laneRole.Equals(expectedRole, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNotEmpty(string value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;

    private static bool IsExpectedLibraryException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException;

    private sealed record LibraryLoadResult(CoachPackLibrarySnapshot? Snapshot);
}
