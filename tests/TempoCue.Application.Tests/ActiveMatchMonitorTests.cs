using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class ActiveMatchMonitorTests
{
    [Fact]
    public async Task RefreshAsync_PublishesOneUpdateToEverySubscriber()
    {
        var source = new CountingSnapshotSource(
            MatchSnapshotReadResult.NoActiveMatch());
        using var monitor = CreateMonitor(source);
        var firstSubscriber = new List<ActiveMatchUpdate>();
        var secondSubscriber = new List<ActiveMatchUpdate>();
        monitor.Updated += (_, e) => firstSubscriber.Add(e.Update);
        monitor.Updated += (_, e) => secondSubscriber.Add(e.Update);

        await monitor.RefreshAsync();

        Assert.Equal(1, source.ReadCount);
        var update = Assert.Single(firstSubscriber);
        Assert.Same(update, Assert.Single(secondSubscriber));
        Assert.Same(update, monitor.CurrentUpdate);
    }

    [Fact]
    public async Task Start_IsIdempotentAndOwnsOnePollingLoop()
    {
        var updateReceived = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new CountingSnapshotSource(
            MatchSnapshotReadResult.NoActiveMatch());
        using var monitor = CreateMonitor(source, TimeSpan.FromHours(1));
        monitor.Updated += (_, _) => updateReceived.TrySetResult();

        monitor.Start();
        monitor.Start();
        await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, source.ReadCount);
    }

    [Fact]
    public async Task RefreshAsync_ContinuesPublishingWhenOneSubscriberFails()
    {
        using var monitor = CreateMonitor(new CountingSnapshotSource(
            MatchSnapshotReadResult.NoActiveMatch()));
        ActiveMatchUpdate? received = null;
        monitor.Updated += (_, _) => throw new InvalidOperationException("Тестовая ошибка");
        monitor.Updated += (_, e) => received = e.Update;

        await monitor.RefreshAsync();

        Assert.Same(monitor.CurrentUpdate, received);
    }

    [Fact]
    public async Task Start_PublishesFailedUpdateWhenPollingLoopStopsUnexpectedly()
    {
        var failedUpdate = new TaskCompletionSource<ActiveMatchUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitor = CreateMonitor(new ThrowingSnapshotSource());
        monitor.Updated += (_, e) =>
        {
            if (e.Update.Status == MatchSnapshotReadStatus.Failed)
            {
                failedUpdate.TrySetResult(e.Update);
            }
        };

        monitor.Start();
        var update = await failedUpdate.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Same(update, monitor.CurrentUpdate);
        Assert.Null(update.Snapshot);
    }

    private static ActiveMatchMonitor CreateMonitor(
        IMatchSnapshotSource source,
        TimeSpan? pollingInterval = null) =>
        new(new ActiveMatchSession(
            source,
            new EmptyAdviceSelector(),
            pollingInterval ?? TimeSpan.FromSeconds(1)));

    private sealed class CountingSnapshotSource(MatchSnapshotReadResult result)
        : IMatchSnapshotSource
    {
        public int ReadCount { get; private set; }

        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class EmptyAdviceSelector : IMatchAdviceSelector
    {
        public ValueTask<ActiveCoachPackSelection> SelectAsync(
            MatchState match,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ActiveCoachPackSelection.NotFound());
    }

    private sealed class ThrowingSnapshotSource : IMatchSnapshotSource
    {
        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromException<MatchSnapshotReadResult>(
                new InvalidOperationException("Тестовая ошибка polling"));
    }
}
