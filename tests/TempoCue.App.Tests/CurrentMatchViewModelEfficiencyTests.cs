using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;
using static TempoCue.App.Tests.CurrentMatchViewModelTestData;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CurrentMatchViewModelEfficiencyTests
{
    [Fact]
    public async Task RefreshAsync_DoesNotRebuildUnchangedRosterAndEvents()
    {
        using var context = new CurrentMatchViewModelTestContext();
        var match = new MatchState
        {
            GameTimeSeconds = 120,
            ActivePlayerId = "urgot-player",
            Players =
            [
                CreatePlayer("urgot-player", "Urgot", TeamSide.Order, PlayerPosition.Top),
                CreatePlayer("sett-player", "Sett", TeamSide.Chaos, PlayerPosition.Top)
            ]
        };
        var snapshot = new MatchSnapshot(
            match,
            [
                new MatchEvent
                {
                    EventId = 1,
                    Kind = MatchEventKind.ChampionKill,
                    GameTimeSeconds = 100,
                    KillerParticipantId = "urgot-player",
                    VictimParticipantId = "sett-player"
                }
            ],
            "{}",
            DateTimeOffset.UtcNow);
        var viewModel = context.CreateViewModel(snapshot);
        await viewModel.RefreshAsync();
        var rosterChanges = 0;
        var eventChanges = 0;
        viewModel.Allies.CollectionChanged += (_, _) => rosterChanges++;
        viewModel.Enemies.CollectionChanged += (_, _) => rosterChanges++;
        viewModel.Events.CollectionChanged += (_, _) => eventChanges++;

        await viewModel.RefreshAsync();

        Assert.Equal(0, rosterChanges);
        Assert.Equal(0, eventChanges);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesEventPresentationWhenMatchDataChanges()
    {
        var firstMatch = new MatchState
        {
            GameTimeSeconds = 120,
            ActivePlayerId = "urgot-player",
            Players =
            [
                CreatePlayer("urgot-player", "Urgot", TeamSide.Order, PlayerPosition.Top),
                CreatePlayer("enemy-player", "Sett", TeamSide.Chaos, PlayerPosition.Top)
            ]
        };
        var secondMatch = firstMatch with
        {
            Players =
            [
                CreatePlayer("urgot-player", "Urgot", TeamSide.Order, PlayerPosition.Top),
                CreatePlayer("enemy-player", "Darius", TeamSide.Chaos, PlayerPosition.Top)
            ]
        };
        var matchEvent = new MatchEvent
        {
            EventId = 1,
            Kind = MatchEventKind.ChampionKill,
            GameTimeSeconds = 100,
            KillerParticipantId = "urgot-player",
            VictimParticipantId = "enemy-player"
        };
        var source = new SequencedSnapshotSource(
            MatchSnapshotReadResult.Success(
                new MatchSnapshot(firstMatch, [matchEvent], "{}", DateTimeOffset.UtcNow)),
            MatchSnapshotReadResult.Success(
                new MatchSnapshot(secondMatch, [matchEvent], "{}", DateTimeOffset.UtcNow)));
        using var viewModel = new CurrentMatchViewModel(
            new ActiveMatchSession(
                source,
                new TestMatchAdviceSelector(),
                TimeSpan.FromSeconds(1)),
            LocalizationService.Instance);

        await viewModel.RefreshAsync();
        Assert.Contains("Sett", Assert.Single(viewModel.Events).Title);

        await viewModel.RefreshAsync();

        Assert.Contains("Darius", Assert.Single(viewModel.Events).Title);
    }

    private sealed class SequencedSnapshotSource(params MatchSnapshotReadResult[] results)
        : IMatchSnapshotSource
    {
        private readonly Queue<MatchSnapshotReadResult> _results = new(results);

        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_results.Dequeue());
    }
}
