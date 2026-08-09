using System.Runtime.CompilerServices;
using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>
/// Единственный владелец цикла опроса активного матча и вычисления совета.
/// Не зависит от WPF, HTTP и формата Riot JSON.
/// </summary>
public sealed class ActiveMatchSession
{
    private readonly IMatchSnapshotSource _snapshotSource;
    private readonly MatchupCoach _coach;
    private readonly TimeSpan _pollingInterval;
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private int _isWatching;

    public ActiveMatchSession(
        IMatchSnapshotSource snapshotSource,
        MatchupCoach coach,
        TimeSpan pollingInterval)
    {
        ArgumentNullException.ThrowIfNull(snapshotSource);
        ArgumentNullException.ThrowIfNull(coach);
        if (pollingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollingInterval),
                "Период опроса должен быть больше нуля.");
        }

        _snapshotSource = snapshotSource;
        _coach = coach;
        _pollingInterval = pollingInterval;
    }

    public async IAsyncEnumerable<ActiveMatchUpdate> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _isWatching, 1) == 1)
        {
            throw new InvalidOperationException("Цикл активной сессии уже запущен.");
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                yield return await RefreshAsync(cancellationToken);
                await Task.Delay(_pollingInterval, cancellationToken);
            }
        }
        finally
        {
            Volatile.Write(ref _isWatching, 0);
        }
    }

    public async Task<ActiveMatchUpdate> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        await _readLock.WaitAsync(cancellationToken);
        try
        {
            var result = await _snapshotSource.ReadAsync(cancellationToken);
            var advice = result.Snapshot is null
                ? null
                : _coach.GetBestAdvice(result.Snapshot.Match);

            return new ActiveMatchUpdate(result.Status, result.Snapshot, advice);
        }
        finally
        {
            _readLock.Release();
        }
    }
}
