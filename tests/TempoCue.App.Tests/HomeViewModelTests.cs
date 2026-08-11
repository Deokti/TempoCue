using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Проверяет представление состояния матча на главной странице.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class HomeViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShowsConnectedMatchSummary()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var match = new MatchState
            {
                GameTimeSeconds = 60,
                ActivePlayerId = "urgot-player",
                Players =
                [
                    new PlayerState
                    {
                        ParticipantId = "urgot-player",
                        ChampionName = "Urgot",
                        Team = TeamSide.Order,
                        Position = PlayerPosition.Top,
                        Level = 1
                    },
                    new PlayerState
                    {
                        ParticipantId = "sett-player",
                        ChampionName = "Sett",
                        Team = TeamSide.Chaos,
                        Position = PlayerPosition.Top,
                        Level = 1
                    }
                ]
            };
            var snapshot = new MatchSnapshot(match, [], "{}", DateTimeOffset.UtcNow);
            var session = new ActiveMatchSession(
                new SnapshotSource(snapshot),
                new TestMatchAdviceSelector(),
                TimeSpan.FromSeconds(1));
            using var currentMatch = new CurrentMatchViewModel(session, localization);
            using var home = new HomeViewModel(currentMatch, localization);

            await home.RefreshAsync();

            Assert.Equal(MatchSnapshotReadStatus.Success, home.Status);
            Assert.Equal("Матч обнаружен", home.HeroTitle);
            Assert.Contains("Urgot", home.HeroDescription);
            Assert.Contains("2 игрока", home.HeroDescription);
            Assert.Equal("подключено", home.LiveDataValue);
            Assert.Equal("идёт", home.MatchStatusValue);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    private sealed class SnapshotSource(MatchSnapshot snapshot) : IMatchSnapshotSource
    {
        public Task<MatchSnapshotReadResult> ReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MatchSnapshotReadResult.Success(snapshot));
    }
}
