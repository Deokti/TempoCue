using TempoCue.Application;
using TempoCue.Core;
using static TempoCue.App.Tests.CurrentMatchViewModelTestData;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CurrentMatchViewModelTests
{
    [Fact]
    public async Task Constructor_AppliesLastUpdatePublishedBeforeSubscription()
    {
        var snapshot = new MatchSnapshot(
            new MatchState
            {
                GameTimeSeconds = 60,
                ActivePlayerId = "urgot-player",
                Players =
                [
                    CreatePlayer(
                        "urgot-player",
                        "Urgot",
                        TeamSide.Order,
                        PlayerPosition.Top)
                ]
            },
            [],
            "{}",
            DateTimeOffset.UtcNow);
        using var monitor = CreateMonitor(TestMatchSnapshotSource.FromSnapshot(snapshot));
        await monitor.RefreshAsync();

        using var viewModel = new TempoCue.App.ViewModels.CurrentMatchViewModel(
            monitor,
            TempoCue.App.Localization.LocalizationService.Instance);

        Assert.Equal(MatchSnapshotReadStatus.Success, viewModel.Status);
        Assert.Equal("Urgot", viewModel.ActiveChampion);
    }

    [Fact]
    public async Task RefreshAsync_BuildsRostersAdviceAndRiotEvents()
    {
        using var context = new CurrentMatchViewModelTestContext();
        var match = new MatchState
        {
            GameTimeSeconds = 610,
            ActivePlayerId = "urgot-player",
            Players =
            [
                new PlayerState
                {
                    ParticipantId = "urgot-player",
                    ChampionName = "Urgot",
                    Team = TeamSide.Order,
                    Position = PlayerPosition.Top,
                    Level = 9,
                    Kills = 2,
                    Deaths = 1,
                    Assists = 3,
                    CreepScore = 88
                },
                new PlayerState
                {
                    ParticipantId = "sett-player",
                    ChampionName = "Sett",
                    Team = TeamSide.Chaos,
                    Position = PlayerPosition.Top,
                    Level = 8,
                    Kills = 1,
                    Deaths = 2,
                    Assists = 0,
                    CreepScore = 74
                }
            ]
        };
        var snapshot = new MatchSnapshot(
            match,
            [
                new MatchEvent
                {
                    EventId = 2,
                    Kind = MatchEventKind.DragonKill,
                    GameTimeSeconds = 600,
                    KillerParticipantId = "urgot-player",
                    DragonType = DragonType.Earth
                },
                new MatchEvent
                {
                    EventId = 1,
                    Kind = MatchEventKind.ChampionKill,
                    GameTimeSeconds = 590,
                    KillerParticipantId = "urgot-player",
                    VictimParticipantId = "sett-player"
                }
            ],
            "{}",
            DateTimeOffset.UtcNow);
        var viewModel = context.CreateViewModel(
            snapshot,
            [
                new CoachRuleDefinition
                {
                    Id = "level-nine",
                    PlayerChampion = "Urgot",
                    OpponentChampion = "Sett",
                    MinimumLevel = 9,
                    PowerState = PowerState.Cautious,
                    Priority = 10,
                    Title = "Дави короткими разменами",
                    Message = "Не затягивай бой.",
                    WhyNow = "Sett уже потратил ключевое умение.",
                    Source = "Тестовый Coach Pack"
                }
            ]);
        var completedUpdates = 0;
        viewModel.StateUpdated += (_, _) => completedUpdates++;

        await viewModel.RefreshAsync();

        Assert.Equal(1, completedUpdates);
        Assert.Equal(MatchSnapshotReadStatus.Success, viewModel.Status);
        Assert.Equal("10:10", viewModel.GameTime);
        Assert.Equal("Urgot", viewModel.ActiveChampion);
        Assert.Equal("2/1/3", viewModel.ActiveKda);
        Assert.Equal("≈88 CS", viewModel.ActiveCreepScore);
        Assert.Equal("Sett", viewModel.OpponentChampion);
        Assert.Equal("Уровень 9", viewModel.MatchupPhase);
        Assert.Equal("Дави короткими разменами", viewModel.AdviceTitle);
        Assert.Equal("Sett уже потратил ключевое умение.", viewModel.AdviceReason);
        Assert.Contains("Тестовый набор", viewModel.AdviceSource);
        Assert.Single(viewModel.Allies);
        Assert.Single(viewModel.Enemies);
        Assert.Equal(2, viewModel.Events.Count);
        Assert.Contains("Горный", viewModel.Events[0].Title);
        Assert.Equal("Urgot убил Sett", viewModel.Events[1].Title);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotShowPackItemForUnknownMatchup()
    {
        using var context = new CurrentMatchViewModelTestContext();
        var match = new MatchState
        {
            GameTimeSeconds = 120,
            ActivePlayerId = "anivia-player",
            Players =
            [
                new PlayerState
                {
                    ParticipantId = "anivia-player",
                    ChampionName = "Anivia",
                    Team = TeamSide.Order,
                    Position = PlayerPosition.Top,
                    Level = 1
                },
                new PlayerState
                {
                    ParticipantId = "teemo-player",
                    ChampionName = "Teemo",
                    Team = TeamSide.Chaos,
                    Position = PlayerPosition.Top,
                    Level = 1
                }
            ]
        };
        var viewModel = context.CreateViewModel(
            new MatchSnapshot(match, [], "{}", DateTimeOffset.UtcNow));

        await viewModel.RefreshAsync();

        Assert.Equal("Teemo", viewModel.OpponentChampion);
        Assert.Equal("Уровень 1", viewModel.MatchupPhase);
        Assert.DoesNotContain("секир", viewModel.MatchupPhase, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("НЕТ ПРАВИЛА", viewModel.PowerStateLabel);
    }

    [Fact]
    public async Task RefreshAsync_ShowsInferredTopAndCurrentFarmInRoster()
    {
        using var context = new CurrentMatchViewModelTestContext();
        var match = new MatchState
        {
            GameTimeSeconds = 360,
            ActivePlayerId = "aatrox-player",
            Players =
            [
                CreatePlayer("aatrox-player", "Aatrox", TeamSide.Order, PlayerPosition.Unknown, 53),
                CreatePlayer("ally-jungle", "Vi", TeamSide.Order, PlayerPosition.Jungle),
                CreatePlayer("ally-middle", "Ahri", TeamSide.Order, PlayerPosition.Middle),
                CreatePlayer("ally-bottom", "Jinx", TeamSide.Order, PlayerPosition.Bottom),
                CreatePlayer("ally-support", "Brand", TeamSide.Order, PlayerPosition.Bottom),
                CreatePlayer("enemy-top", "Teemo", TeamSide.Chaos, PlayerPosition.Top),
                CreatePlayer("enemy-jungle", "Amumu", TeamSide.Chaos, PlayerPosition.Jungle),
                CreatePlayer("enemy-middle", "Annie", TeamSide.Chaos, PlayerPosition.Middle),
                CreatePlayer("enemy-bottom", "Caitlyn", TeamSide.Chaos, PlayerPosition.Bottom),
                CreatePlayer("enemy-support", "Lux", TeamSide.Chaos, PlayerPosition.Bottom)
            ]
        };
        var viewModel = context.CreateViewModel(
            new MatchSnapshot(match, [], "{}", DateTimeOffset.UtcNow));

        await viewModel.RefreshAsync();

        var activePlayer = Assert.Single(
            viewModel.Allies,
            player => player.IsActivePlayer);
        Assert.Equal("TOP", activePlayer.RoleCode);
        Assert.Equal("Aatrox", viewModel.Allies[0].ChampionName);
        Assert.Equal("вы · ≈53 CS", activePlayer.Details);
        Assert.Equal("Верхняя линия", viewModel.ActivePosition);
        Assert.Equal("Teemo", viewModel.OpponentChampion);
    }

    [Fact]
    public async Task RefreshAsync_ShowsLoadingInsteadOfConnectionError()
    {
        using var context = new CurrentMatchViewModelTestContext();
        var viewModel = context.CreateViewModel(MatchSnapshotReadResult.GameLoading());

        await viewModel.RefreshAsync();

        Assert.Equal(MatchSnapshotReadStatus.GameLoading, viewModel.Status);
        Assert.Equal("ЗАГРУЗКА МАТЧА", viewModel.ConnectionTitle);
        Assert.Contains("ожидаем готовность", viewModel.ConnectionDetails);
    }

    [Fact]
    public async Task RefreshAsync_ExplainsConflictingEnabledPacks()
    {
        using var context = new CurrentMatchViewModelTestContext();
        var match = new MatchState
        {
            GameTimeSeconds = 540,
            ActivePlayerId = "urgot-player",
            Players =
            [
                CreatePlayer("urgot-player", "Urgot", TeamSide.Order, PlayerPosition.Top),
                CreatePlayer("sett-player", "Sett", TeamSide.Chaos, PlayerPosition.Top)
            ]
        };
        var selection = ActiveCoachPackSelection.Conflict(["Первый", "Второй"]);
        var viewModel = context.CreateViewModel(
            new MatchSnapshot(match, [], "{}", DateTimeOffset.UtcNow),
            new TestMatchAdviceSelector(selection));

        await viewModel.RefreshAsync();

        Assert.Equal("КОНФЛИКТ НАБОРОВ", viewModel.PowerStateLabel);
        Assert.Contains("Первый, Второй", viewModel.AdviceMessage);
        Assert.Contains("не выбирает рекомендацию случайно", viewModel.AdviceReason);
    }

}
