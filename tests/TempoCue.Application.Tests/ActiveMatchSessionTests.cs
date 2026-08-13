using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class ActiveMatchSessionTests
{
    [Fact]
    public async Task RefreshAsync_ReturnsAdviceForSuccessfulSnapshot()
    {
        var match = CreateMatch(level: 9);
        var snapshot = new MatchSnapshot(match, [], DateTimeOffset.UtcNow);
        var source = new StubSnapshotSource(MatchSnapshotReadResult.Success(snapshot));
        var coach = new MatchupCoach(
        [
            new CoachRuleDefinition
            {
                Id = "level-nine",
                PlayerChampion = "Urgot",
                OpponentChampion = "Sett",
                MinimumLevel = 9,
                PowerState = PowerState.Cautious,
                Priority = 10,
                Title = "Короткие размены",
                Message = "Не затягивай бой.",
                Source = "Тест"
            }
        ]);
        var session = new ActiveMatchSession(
            source,
            new FixedAdviceSelector(coach),
            TimeSpan.FromSeconds(1));

        var update = await session.RefreshAsync();

        Assert.Equal(MatchSnapshotReadStatus.Success, update.Status);
        Assert.Same(snapshot, update.Snapshot);
        Assert.Equal("level-nine", update.Advice?.RuleId);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsMapControlPlanForSuccessfulSnapshot()
    {
        var snapshot = new MatchSnapshot(
            CreateMatch(level: 9),
            [],
            DateTimeOffset.UtcNow);
        var expectedPlan = new ActiveMapControlPlan([], ["Контроль реки"]);
        var session = new ActiveMatchSession(
            new StubSnapshotSource(MatchSnapshotReadResult.Success(snapshot)),
            new FixedAdviceSelector(new MatchupCoach([])),
            TimeSpan.FromSeconds(1),
            new FixedMapControlPlanSelector(expectedPlan));

        var update = await session.RefreshAsync();

        Assert.Same(expectedPlan, update.MapControlPlan);
    }

    [Fact]
    public async Task RefreshAsync_PreservesExpectedNoMatchState()
    {
        var source = new StubSnapshotSource(MatchSnapshotReadResult.NoActiveMatch());
        var session = new ActiveMatchSession(
            source,
            new FixedAdviceSelector(new MatchupCoach([])),
            TimeSpan.FromSeconds(1));

        var update = await session.RefreshAsync();

        Assert.Equal(MatchSnapshotReadStatus.NoActiveMatch, update.Status);
        Assert.Null(update.Snapshot);
        Assert.Null(update.Advice);
    }

    [Fact]
    public void Constructor_RejectsNonPositivePollingInterval()
    {
        var source = new StubSnapshotSource(MatchSnapshotReadResult.NoActiveMatch());

        Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveMatchSession(
            source,
            new FixedAdviceSelector(new MatchupCoach([])),
            TimeSpan.Zero));
    }

    private static MatchState CreateMatch(int level)
    {
        return new MatchState
        {
            GameTimeSeconds = 540,
            ActivePlayerId = "player",
            Players =
            [
                new PlayerState
                {
                    ParticipantId = "player",
                    ChampionName = "Urgot",
                    Team = TeamSide.Order,
                    Position = PlayerPosition.Top,
                    Level = level
                },
                new PlayerState
                {
                    ParticipantId = "opponent",
                    ChampionName = "Sett",
                    Team = TeamSide.Chaos,
                    Position = PlayerPosition.Top,
                    Level = level
                }
            ]
        };
    }

    private sealed class StubSnapshotSource(MatchSnapshotReadResult result)
        : IMatchSnapshotSource
    {
        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FixedAdviceSelector(MatchupCoach coach) : IMatchAdviceSelector
    {
        public ValueTask<ActiveCoachPackSelection> SelectAsync(
            MatchState match,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ActiveCoachPackSelection.Selected(
                new CoachPack
                {
                    SchemaVersion = 1,
                    Id = "test",
                    Name = "Test",
                    Patch = "test"
                },
                coach.GetBestAdvice(match)));
    }

    private sealed class FixedMapControlPlanSelector(ActiveMapControlPlan plan)
        : IMapControlPlanSelector
    {
        public ValueTask<ActiveMapControlPlan> SelectAsync(
            MatchSnapshot snapshot,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(plan);
    }
}
