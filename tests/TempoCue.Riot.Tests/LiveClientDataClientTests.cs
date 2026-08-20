using System.Net;
using System.Collections.Concurrent;
using System.Text;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.Riot.Tests;

/// <summary>
/// Проверяет обработку сохранённого ответа Riot без необходимости запускать игру.
/// </summary>
public sealed class LiveClientDataClientTests
{
    [Fact]
    public async Task GetSnapshotAsync_MapsActiveMatchFromResponseStream()
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
                  "scores": { "kills": 2, "deaths": 1, "assists": 3, "creepScore": 91 },
                  "isDead": false,
                  "respawnTimer": 0,
                  "isBot": false
                },
                {
                  "riotIdGameName": "AllySupport",
                  "riotIdTagLine": "TEST",
                  "summonerName": "AllySupport",
                  "championName": "Треш",
                  "rawChampionName": "game_character_displayname_Thresh",
                  "team": "ORDER",
                  "position": "UTILITY",
                  "level": 8,
                  "items": [],
                  "scores": { "kills": 0, "deaths": 2, "assists": 5, "creepScore": 11 },
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
                  "scores": { "kills": 1, "deaths": 2, "assists": 0, "creepScore": 80 },
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
                "Events": [
                  {
                    "EventID": 7,
                    "EventName": "ChampionKill",
                    "EventTime": 612.5,
                    "KillerName": "UrgotPlayer",
                    "VictimName": "SettPlayer",
                    "Assisters": ["AllySupport"]
                  },
                  {
                    "EventID": 8,
                    "EventName": "DragonKill",
                    "EventTime": 620,
                    "DragonType": "Earth",
                    "KillerName": "UrgotPlayer"
                  },
                  {
                    "EventID": 9,
                    "EventName": "HeraldKill",
                    "EventTime": 630,
                    "KillerName": "UrgotPlayer"
                  },
                  {
                    "EventID": 10,
                    "EventName": "BaronKill",
                    "EventTime": 640,
                    "KillerName": "UrgotPlayer"
                  }
                ]
              }
            }
            """;

        var handler = new StubHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        using var client = new LiveClientDataClient(httpClient);

        var snapshot = await client.GetSnapshotAsync();

        Assert.Contains(LiveClientDataClient.AllGameDataEndpoint, handler.RequestedUris);
        Assert.Single(handler.RequestedUris);
        Assert.Equal(645.25, snapshot.Match.GameTimeSeconds);
        Assert.Equal("UrgotPlayer#TEST", snapshot.Match.ActivePlayerId);
        Assert.Equal(3, snapshot.Match.Players.Count);

        var activePlayer = snapshot.Match.GetActivePlayer();
        Assert.NotNull(activePlayer);
        Assert.Equal("UrgotPlayer#TEST", activePlayer.RiotId?.DisplayName);
        Assert.Equal("Urgot", activePlayer.ChampionName);
        Assert.Equal(PlayerPosition.Top, activePlayer.Position);
        Assert.Contains(3071, activePlayer.ItemIds);
        Assert.Equal(2, activePlayer.Kills);
        Assert.Equal(1, activePlayer.Deaths);
        Assert.Equal(3, activePlayer.Assists);
        Assert.Equal(91, activePlayer.CreepScore);

        var support = Assert.Single(snapshot.Match.GetAllies());
        Assert.Equal("AllySupport#TEST", support.RiotId?.DisplayName);
        Assert.Equal(PlayerPosition.Support, support.Position);
        Assert.Equal(11, support.CreepScore);

        var sett = Assert.Single(snapshot.Match.GetEnemies());
        Assert.Equal("SettPlayer#TEST", sett.RiotId?.DisplayName);
        Assert.Equal("Sett", sett.ChampionName);
        Assert.Equal(80, sett.CreepScore);
        Assert.True(sett.IsDead);
        Assert.Equal(12.5, sett.RespawnTimerSeconds);

        Assert.Equal(4, snapshot.Events.Count);
        var baron = snapshot.Events[0];
        Assert.Equal(MatchEventKind.BaronNashorKill, baron.Kind);
        var herald = snapshot.Events[1];
        Assert.Equal(MatchEventKind.RiftHeraldKill, herald.Kind);
        var dragon = snapshot.Events[2];
        Assert.Equal(MatchEventKind.DragonKill, dragon.Kind);
        Assert.Equal(DragonType.Earth, dragon.DragonType);
        Assert.Equal("UrgotPlayer#TEST", dragon.KillerParticipantId);

        var championKill = snapshot.Events[3];
        Assert.Equal(MatchEventKind.ChampionKill, championKill.Kind);
        Assert.Equal("UrgotPlayer#TEST", championKill.KillerParticipantId);
        Assert.Equal("SettPlayer#TEST", championKill.VictimParticipantId);
        Assert.Equal(["AllySupport"], championKill.AssisterParticipantIds);

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

        var result = await client.ReadAsync();
        Assert.Equal(MatchSnapshotReadStatus.InvalidData, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReadsAllPlayerScoresFromFullSnapshot()
    {
        const string json = """
            {
              "activePlayer": {
                "riotId": "AatroxPlayer#TEST",
                "summonerName": "AatroxPlayer"
              },
              "allPlayers": [
                {
                  "riotId": "AatroxPlayer#TEST",
                  "summonerName": "AatroxPlayer",
                  "championName": "Aatrox",
                  "team": "ORDER",
                  "position": "TOP",
                  "level": 7,
                  "scores": { "kills": 0, "deaths": 0, "assists": 0, "creepScore": 53 }
                },
                {
                  "riotId": "TeemoBot#BOT",
                  "summonerName": "TeemoBot",
                  "championName": "Teemo",
                  "team": "CHAOS",
                  "position": "TOP",
                  "level": 7,
                  "scores": { "kills": 0, "deaths": 0, "assists": 0, "creepScore": 48 }
                }
              ],
              "gameData": { "gameTime": 420 }
            }
            """;
        var handler = new StubHttpMessageHandler(json);
        using var httpClient = new HttpClient(handler);
        using var client = new LiveClientDataClient(httpClient);

        var snapshot = await client.GetSnapshotAsync();

        Assert.Single(handler.RequestedUris);
        Assert.Equal(53, snapshot.Match.GetActivePlayer()?.CreepScore);
        Assert.Equal(48, Assert.Single(snapshot.Match.GetEnemies()).CreepScore);
    }

    [Fact]
    public async Task GetSnapshotAsync_DeserializesWithoutBufferingWholeResponse()
    {
        const string json = """
            {
              "activePlayer": {
                "riotId": "StreamPlayer#TEST",
                "summonerName": "StreamPlayer"
              },
              "allPlayers": [
                {
                  "riotId": "StreamPlayer#TEST",
                  "summonerName": "StreamPlayer",
                  "championName": "Urgot",
                  "team": "ORDER",
                  "position": "TOP",
                  "level": 1,
                  "scores": { "kills": 0, "deaths": 0, "assists": 0, "creepScore": 0 }
                }
              ],
              "gameData": { "gameTime": 1 }
            }
            """;
        var handler = new StubHttpMessageHandler(
            json,
            contentFactory: () => new StreamOnlyHttpContent(json));
        using var httpClient = new HttpClient(handler);
        using var client = new LiveClientDataClient(httpClient);

        var snapshot = await client.GetSnapshotAsync();

        Assert.Equal("StreamPlayer#TEST", snapshot.Match.ActivePlayerId);
        Assert.Single(snapshot.Match.Players);
    }

    [Fact]
    public async Task ReadAsync_ReturnsGameLoadingWhenProcessIsRunning()
    {
        var handler = new StubHttpMessageHandler(
            "{}",
            allGameStatusCode: HttpStatusCode.ServiceUnavailable);
        using var httpClient = new HttpClient(handler);
        using var client = new LiveClientDataClient(httpClient, static () => true);

        var result = await client.ReadAsync();

        Assert.Equal(MatchSnapshotReadStatus.GameLoading, result.Status);
        Assert.Null(result.Snapshot);
        Assert.Single(handler.RequestedUris);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNoMatchWhenGameProcessIsAbsent()
    {
        var handler = new StubHttpMessageHandler(
            "{}",
            allGameStatusCode: HttpStatusCode.ServiceUnavailable);
        using var httpClient = new HttpClient(handler);
        using var client = new LiveClientDataClient(httpClient, static () => false);

        var result = await client.ReadAsync();

        Assert.Equal(MatchSnapshotReadStatus.NoActiveMatch, result.Status);
        Assert.Null(result.Snapshot);
        Assert.Empty(handler.RequestedUris);
    }

    private sealed class StubHttpMessageHandler(
        string responseJson,
        HttpStatusCode allGameStatusCode = HttpStatusCode.OK,
        Func<HttpContent>? contentFactory = null) : HttpMessageHandler
    {
        public ConcurrentBag<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri
                ?? throw new InvalidOperationException("Тестовый запрос не содержит URI.");
            RequestedUris.Add(requestUri);

            if (allGameStatusCode != HttpStatusCode.OK)
            {
                return Task.FromResult(new HttpResponseMessage(allGameStatusCode));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = contentFactory?.Invoke()
                    ?? new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class StreamOnlyHttpContent(string json) : HttpContent
    {
        private readonly byte[] _payload = Encoding.UTF8.GetBytes(json);

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            throw new InvalidOperationException(
                "Полная буферизация тестового HTTP-ответа запрещена.");

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new MemoryStream(_payload, writable: false));

        protected override bool TryComputeLength(out long length)
        {
            length = _payload.Length;
            return true;
        }
    }
}
