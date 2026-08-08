using System.Net;
using System.Text;
using RiftCoach.Core;

namespace RiftCoach.Riot.Tests;

/// <summary>
/// Проверяет обработку сохранённого ответа Riot без необходимости запускать игру.
/// </summary>
public sealed class LiveClientDataClientTests
{
    [Fact]
    public async Task GetSnapshotAsync_MapsActiveMatchAndPreservesJson()
    {
        const string json = """
            {
              "activePlayer": {
                "riotId": "UrgotPlayer#TEST",
                "summonerName": "UrgotPlayer"
              },
              "allPlayers": [
                {
                  "riotId": "UrgotPlayer#TEST",
                  "summonerName": "UrgotPlayer",
                  "championName": "Ургот",
                  "rawChampionName": "game_character_displayname_Urgot",
                  "team": "ORDER",
                  "position": "TOP",
                  "level": 9,
                  "items": [
                    { "itemID": 3071 }
                  ],
                  "isDead": false,
                  "respawnTimer": 0,
                  "isBot": false
                },
                {
                  "riotId": "AllySupport#TEST",
                  "summonerName": "AllySupport",
                  "championName": "Треш",
                  "rawChampionName": "game_character_displayname_Thresh",
                  "team": "ORDER",
                  "position": "UTILITY",
                  "level": 8,
                  "items": [],
                  "isDead": false,
                  "respawnTimer": 0,
                  "isBot": false
                },
                {
                  "riotId": "SettPlayer#TEST",
                  "summonerName": "SettPlayer",
                  "championName": "Сетт",
                  "rawChampionName": "game_character_displayname_Sett",
                  "team": "CHAOS",
                  "position": "TOP",
                  "level": 9,
                  "items": [],
                  "isDead": true,
                  "respawnTimer": 12.5,
                  "isBot": false
                }
              ],
              "gameData": {
                "gameTime": 645.25,
                "gameMode": "CLASSIC",
                "mapName": "Map11"
              },
              "events": {
                "Events": []
              }
            }
            """;

        var handler = new StubHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        using var client = new LiveClientDataClient(httpClient);

        var snapshot = await client.GetSnapshotAsync();

        Assert.Equal(LiveClientDataClient.AllGameDataEndpoint, handler.LastRequestUri);
        Assert.Equal(645.25, snapshot.Match.GameTimeSeconds);
        Assert.Equal("UrgotPlayer#TEST", snapshot.Match.ActivePlayerId);
        Assert.Equal(3, snapshot.Match.Players.Count);

        var activePlayer = snapshot.Match.GetActivePlayer();
        Assert.NotNull(activePlayer);
        Assert.Equal("Urgot", activePlayer.ChampionName);
        Assert.Equal(PlayerPosition.Top, activePlayer.Position);
        Assert.Contains(3071, activePlayer.ItemIds);

        var support = Assert.Single(snapshot.Match.GetAllies());
        Assert.Equal(PlayerPosition.Support, support.Position);

        var sett = Assert.Single(snapshot.Match.GetEnemies());
        Assert.Equal("Sett", sett.ChampionName);
        Assert.True(sett.IsDead);
        Assert.Equal(12.5, sett.RespawnTimerSeconds);

        Assert.Contains(Environment.NewLine, snapshot.RawJson);
        Assert.Contains("\"allPlayers\"", snapshot.RawJson);

        // Проверяем всю цепочку: JSON Riot → MatchState → определение Sett на TOP → совет.
        var coach = new MatchupCoach(
        [
            new CoachRuleDefinition
            {
                Id = "riot-live-power-spike",
                PlayerChampion = "Urgot",
                OpponentChampion = "Sett",
                MinimumLevel = 9,
                RequiredItemIds = [3071],
                PowerState = PowerState.Playable,
                Priority = 100,
                Title = "Тестовый совет",
                Message = "Тестовое сообщение",
                Source = "Автоматический тест"
            }
        ]);

        var advice = coach.GetBestAdvice(snapshot.Match);

        Assert.NotNull(advice);
        Assert.Equal("riot-live-power-spike", advice.RuleId);
    }

    [Fact]
    public async Task GetSnapshotAsync_RejectsResponseWithoutActivePlayerId()
    {
        const string json = """
            {
              "activePlayer": {},
              "allPlayers": [],
              "gameData": { "gameTime": 0 }
            }
            """;

        var handler = new StubHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        using var client = new LiveClientDataClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => client.GetSnapshotAsync());

        Assert.Contains("активного игрока", exception.Message);
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
