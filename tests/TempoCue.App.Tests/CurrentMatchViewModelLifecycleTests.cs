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
        var completion = new TaskCompletionSource<MatchSnapshotReadResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitor = CreateMonitor(
            new TestMatchSnapshotSource(_ => completion.Task),
            new TestMatchAdviceSelector());
        var viewModel = new CurrentMatchViewModel(
            monitor,
            TestChampionCatalog.Ready(),
            LocalizationService.Instance);
        var completedUpdates = 0;
        viewModel.StateUpdated += (_, _) => completedUpdates++;
        var refresh = viewModel.RefreshAsync();

        viewModel.Dispose();
        completion.SetResult(MatchSnapshotReadResult.Success(new MatchSnapshot(
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
            DateTimeOffset.UtcNow)));
        await refresh;

        Assert.Equal(0, completedUpdates);
        Assert.Equal("—", viewModel.ActiveChampion);
    }
}
