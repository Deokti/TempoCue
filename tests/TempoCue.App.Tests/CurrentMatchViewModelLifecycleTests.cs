using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;
using static TempoCue.App.Tests.CurrentMatchViewModelTestData;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CurrentMatchViewModelLifecycleTests
{
    [Fact]
    public async Task Dispose_PreventsPendingRefreshFromUpdatingViewModel()
    {
        var source = new DeferredSnapshotSource();
        var session = new ActiveMatchSession(
            source,
            new TestMatchAdviceSelector(),
            TimeSpan.FromSeconds(1));
        var viewModel = new CurrentMatchViewModel(
            session,
            LocalizationService.Instance);
        var completedUpdates = 0;
        viewModel.StateUpdated += (_, _) => completedUpdates++;
        var refresh = viewModel.RefreshAsync();

        viewModel.Dispose();
        source.Complete(MatchSnapshotReadResult.Success(new MatchSnapshot(
            new MatchState
            {
                GameTimeSeconds = 120,
                ActivePlayerId = "urgot-player",
                Players =
                [
                    CreatePlayer("urgot-player", "Urgot", TeamSide.Order, PlayerPosition.Top)
                ]
            },
            [],
            "{}",
            DateTimeOffset.UtcNow)));
        await refresh;

        Assert.Equal(0, completedUpdates);
        Assert.Equal("—", viewModel.ActiveChampion);
    }

    private sealed class DeferredSnapshotSource : IMatchSnapshotSource
    {
        private readonly TaskCompletionSource<MatchSnapshotReadResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            _completion.Task;

        public void Complete(MatchSnapshotReadResult result) =>
            _completion.TrySetResult(result);
    }
}
