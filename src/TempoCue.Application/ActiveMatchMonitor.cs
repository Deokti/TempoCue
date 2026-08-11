using System.Diagnostics;

namespace TempoCue.Application;

/// <summary>
/// Владеет единственным циклом обновления активного матча и публикует готовые
/// снимки экрану матча, будущему оверлею и другим независимым потребителям.
/// </summary>
public sealed class ActiveMatchMonitor : IActiveMatchMonitor, IDisposable
{
    private readonly ActiveMatchSession _session;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _lifecycleLock = new();

    private Task? _watchTask;
    private ActiveMatchUpdate? _currentUpdate;
    private bool _isDisposed;

    public ActiveMatchMonitor(ActiveMatchSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public event EventHandler<ActiveMatchUpdateEventArgs>? Updated;

    public ActiveMatchUpdate? CurrentUpdate => Volatile.Read(ref _currentUpdate);

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _watchTask ??= WatchAsync(_lifetimeCancellation.Token);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        var update = await _session.RefreshAsync(linkedCancellation.Token);
        Publish(update);
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _lifetimeCancellation.Cancel();
            Updated = null;
        }

        // CTS намеренно не освобождается синхронно: внешний источник снимка может
        // зарегистрировать отмену уже после того, как Dispose инициировал выход.
        // Монитор живёт столько же, сколько приложение, и не запрашивает WaitHandle.
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var update in _session.WatchAsync(cancellationToken))
            {
                Publish(update);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Завершение приложения штатно останавливает общий цикл polling.
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "Цикл активного матча завершился с ошибкой ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            Publish(new ActiveMatchUpdate(
                MatchSnapshotReadStatus.Failed,
                null,
                null));
        }
    }

    private void Publish(ActiveMatchUpdate update)
    {
        if (_isDisposed)
        {
            return;
        }

        Volatile.Write(ref _currentUpdate, update);
        var arguments = new ActiveMatchUpdateEventArgs(update);
        foreach (EventHandler<ActiveMatchUpdateEventArgs> handler in
                 Updated?.GetInvocationList().Cast<EventHandler<ActiveMatchUpdateEventArgs>>()
                 ?? [])
        {
            try
            {
                handler(this, arguments);
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "Потребитель обновления матча завершился с ошибкой ({0}): {1}",
                    exception.GetType().Name,
                    exception.Message);
            }
        }
    }
}
