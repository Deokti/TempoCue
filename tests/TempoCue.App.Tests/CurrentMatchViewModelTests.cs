using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CurrentMatchViewModelTests
{
    [Fact]
    public async Task RefreshAsync_BuildsRostersAdviceAndRiotEvents()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
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
                    Title = "Дави короткими разменами",
                    Message = "Не затягивай бой.",
                    Source = "Тестовый Coach Pack"
                }
            ]);
            var session = new ActiveMatchSession(
                new SnapshotSource(snapshot),
                coach,
                TimeSpan.FromSeconds(1));
            using var viewModel = new CurrentMatchViewModel(session, localization);
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
            Assert.Single(viewModel.Allies);
            Assert.Single(viewModel.Enemies);
            Assert.Equal(2, viewModel.Events.Count);
            Assert.Contains("Горный", viewModel.Events[0].Title);
            Assert.Equal("Urgot убил Sett", viewModel.Events[1].Title);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Fact]
    public async Task RefreshAsync_DoesNotShowPackItemForUnknownMatchup()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
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
            var snapshot = new MatchSnapshot(
                match,
                [],
                "{}",
                DateTimeOffset.UtcNow);
            var session = new ActiveMatchSession(
                new SnapshotSource(snapshot),
                new MatchupCoach([]),
                TimeSpan.FromSeconds(1));
            using var viewModel = new CurrentMatchViewModel(session, localization);

            await viewModel.RefreshAsync();

            Assert.Equal("Teemo", viewModel.OpponentChampion);
            Assert.Equal("Уровень 1", viewModel.MatchupPhase);
            Assert.DoesNotContain("секир", viewModel.MatchupPhase, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("НЕТ ПРАВИЛА", viewModel.PowerStateLabel);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Fact]
    public async Task RefreshAsync_ShowsInferredTopAndCurrentFarmInRoster()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
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
            var snapshot = new MatchSnapshot(match, [], "{}", DateTimeOffset.UtcNow);
            var session = new ActiveMatchSession(
                new SnapshotSource(snapshot),
                new MatchupCoach([]),
                TimeSpan.FromSeconds(1));
            using var viewModel = new CurrentMatchViewModel(session, localization);

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
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Fact]
    public async Task RefreshAsync_ShowsLoadingInsteadOfConnectionError()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var session = new ActiveMatchSession(
                new ResultSource(MatchSnapshotReadResult.GameLoading()),
                new MatchupCoach([]),
                TimeSpan.FromSeconds(1));
            using var viewModel = new CurrentMatchViewModel(session, localization);

            await viewModel.RefreshAsync();

            Assert.Equal(MatchSnapshotReadStatus.GameLoading, viewModel.Status);
            Assert.Equal("ЗАГРУЗКА МАТЧА", viewModel.ConnectionTitle);
            Assert.Contains("ожидаем готовность", viewModel.ConnectionDetails);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    private static PlayerState CreatePlayer(
        string id,
        string champion,
        TeamSide team,
        PlayerPosition position,
        int creepScore = 0) => new()
        {
            ParticipantId = id,
            ChampionName = champion,
            Team = team,
            Position = position,
            Level = 1,
            CreepScore = creepScore
        };

    private sealed class SnapshotSource(MatchSnapshot snapshot) : IMatchSnapshotSource
    {
        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MatchSnapshotReadResult.Success(snapshot));
    }

    private sealed class ResultSource(MatchSnapshotReadResult result) : IMatchSnapshotSource
    {
        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
