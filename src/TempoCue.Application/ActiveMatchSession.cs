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
    private readonly IMatchAdviceSelector _adviceSelector;
    private readonly TimeSpan _pollingInterval;
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private int _isWatching;

    public ActiveMatchSession(
        IMatchSnapshotSource snapshotSource,
        IMatchAdviceSelector adviceSelector,
        TimeSpan pollingInterval)
    {
        ArgumentNullException.ThrowIfNull(snapshotSource);
        ArgumentNullException.ThrowIfNull(adviceSelector);
        if (pollingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollingInterval),
                "Период опроса должен быть больше нуля.");
        }

        _snapshotSource = snapshotSource;
        _adviceSelector = adviceSelector;
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
            var selection = result.Snapshot is null
                ? null
                : await _adviceSelector.SelectAsync(
                    result.Snapshot.Match,
                    cancellationToken);

            return new ActiveMatchUpdate(result.Status, result.Snapshot, selection);
        }
        finally
        {
            _readLock.Release();
        }
    }
}
