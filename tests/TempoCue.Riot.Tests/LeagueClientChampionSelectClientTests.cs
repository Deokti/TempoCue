using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.Riot.Tests;

public sealed class LeagueClientChampionSelectClientTests
{
    private static readonly LeagueClientConnection Connection = new(
        new Uri("https://127.0.0.1:45678/"),
        "test-password");

    [Fact]
    public async Task ReadAsync_ResolvesOnlyExplicitlyVisibleIdentity()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            AssertBasicAuthorization(request.Headers.Authorization);
            return request.RequestUri?.AbsolutePath switch
            {
                "/lol-champ-select/v1/session" => JsonResponse("""
                    {
                      "localPlayerCellId": 0,
                      "myTeam": [
                        {
                          "cellId": 0,
                          "championId": 6,
                          "summonerId": 101,
                          "assignedPosition": "top",
                          "nameVisibilityType": "VISIBLE"
                        },
                        {
                          "cellId": 1,
                          "championId": 266,
                          "summonerId": 202,
                          "assignedPosition": "jungle",
                          "nameVisibilityType": "HIDDEN"
                        }
                      ]
                    }
                    """),
                "/lol-summoner/v1/summoners/101" => JsonResponse("""
                    { "gameName": "Tempo Player", "tagLine": "TEST" }
                    """),
                _ => throw new InvalidOperationException(
                    $"Неожиданный запрос: {request.RequestUri}")
            };
        });
        using var client = CreateClient(handler);

        var result = await client.ReadAsync();

        Assert.Equal(ChampionSelectReadStatus.Success, result.Status);
        var state = Assert.IsType<ChampionSelectState>(result.State);
        var visible = state.Participants[0];
        Assert.Equal(ChampionSelectIdentityVisibility.Visible, visible.IdentityVisibility);
        Assert.Equal("Tempo Player#TEST", visible.RiotId?.DisplayName);
        var anonymous = state.Participants[1];
        Assert.Equal(ChampionSelectIdentityVisibility.Anonymous, anonymous.IdentityVisibility);
        Assert.Null(anonymous.RiotId);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task ReadAsync_DoesNotResolveIdentityWhenVisibilityIsMissing()
    {
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri?.AbsolutePath == "/lol-champ-select/v1/session"
                ? JsonResponse("""
                    {
                      "localPlayerCellId": 0,
                      "myTeam": [
                        { "cellId": 0, "championId": 6, "summonerId": 101 }
                      ]
                    }
                    """)
                : throw new InvalidOperationException("Профиль не должен запрашиваться."));
        using var client = CreateClient(handler);

        var result = await client.ReadAsync();

        var participant = Assert.Single(Assert.IsType<ChampionSelectState>(result.State).Participants);
        Assert.Equal(ChampionSelectIdentityVisibility.Unavailable, participant.IdentityVisibility);
        Assert.Null(participant.RiotId);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ReadAsync_DoesNotResolveIdentityForUnknownVisibilityValue()
    {
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri?.AbsolutePath == "/lol-champ-select/v1/session"
                ? JsonResponse("""
                    {
                      "localPlayerCellId": 0,
                      "myTeam": [
                        {
                          "cellId": 0,
                          "championId": 6,
                          "summonerId": 101,
                          "nameVisibilityType": "UNSET"
                        }
                      ]
                    }
                    """)
                : throw new InvalidOperationException("Профиль не должен запрашиваться."));
        using var client = CreateClient(handler);

        var result = await client.ReadAsync();

        var participant = Assert.Single(Assert.IsType<ChampionSelectState>(result.State).Participants);
        Assert.Equal(ChampionSelectIdentityVisibility.Unavailable, participant.IdentityVisibility);
        Assert.Null(participant.RiotId);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ReadAsync_CachesVisibleIdentityOnlyForCurrentSession()
    {
        var sessionAvailable = true;
        var handler = new StubHttpMessageHandler(request => request.RequestUri?.AbsolutePath switch
        {
            "/lol-champ-select/v1/session" when sessionAvailable => JsonResponse("""
                {
                  "localPlayerCellId": 0,
                  "myTeam": [
                    {
                      "cellId": 0,
                      "championId": 6,
                      "summonerId": 101,
                      "nameVisibilityType": "VISIBLE"
                    }
                  ]
                }
                """),
            "/lol-champ-select/v1/session" =>
                new HttpResponseMessage(HttpStatusCode.NotFound),
            "/lol-summoner/v1/summoners/101" => JsonResponse("""
                { "gameName": "Tempo Player", "tagLine": "TEST" }
                """),
            _ => throw new InvalidOperationException()
        });
        using var client = CreateClient(handler);

        await client.ReadAsync();
        await client.ReadAsync();
        sessionAvailable = false;
        var ended = await client.ReadAsync();

        Assert.Equal(ChampionSelectReadStatus.NotInChampionSelect, ended.Status);
        Assert.Equal(3, handler.SessionRequestCount);
        Assert.Equal(1, handler.SummonerRequestCount);
    }

    [Fact]
    public async Task ReadAsync_ResolvesVisibleIdentityAgainAfterSessionEnds()
    {
        var sessionCall = 0;
        var handler = new StubHttpMessageHandler(request => request.RequestUri?.AbsolutePath switch
        {
            "/lol-champ-select/v1/session" when ++sessionCall == 2 =>
                new HttpResponseMessage(HttpStatusCode.NotFound),
            "/lol-champ-select/v1/session" => JsonResponse("""
                {
                  "localPlayerCellId": 0,
                  "myTeam": [
                    {
                      "cellId": 0,
                      "championId": 6,
                      "summonerId": 101,
                      "nameVisibilityType": "VISIBLE"
                    }
                  ]
                }
                """),
            "/lol-summoner/v1/summoners/101" => JsonResponse("""
                { "gameName": "Tempo Player", "tagLine": "TEST" }
                """),
            _ => throw new InvalidOperationException()
        });
        using var client = CreateClient(handler);

        await client.ReadAsync();
        await client.ReadAsync();
        await client.ReadAsync();

        Assert.Equal(3, handler.SessionRequestCount);
        Assert.Equal(2, handler.SummonerRequestCount);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNotInChampionSelectForNotFound()
    {
        using var client = CreateClient(new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await client.ReadAsync();

        Assert.Equal(ChampionSelectReadStatus.NotInChampionSelect, result.Status);
        Assert.Null(result.State);
    }

    private static LeagueClientChampionSelectClient CreateClient(
        HttpMessageHandler handler) => new(
        new HttpClient(handler),
        _ => Task.FromResult<LeagueClientConnection?>(Connection));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static void AssertBasicAuthorization(AuthenticationHeaderValue? authorization)
    {
        Assert.NotNull(authorization);
        Assert.Equal("Basic", authorization.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:test-password")),
            authorization.Parameter);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public int SessionRequestCount { get; private set; }

        public int SummonerRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.RequestUri?.AbsolutePath == "/lol-champ-select/v1/session")
            {
                SessionRequestCount++;
            }
            else if (request.RequestUri?.AbsolutePath.StartsWith(
                         "/lol-summoner/v1/summoners/",
                         StringComparison.Ordinal) == true)
            {
                SummonerRequestCount++;
            }

            return Task.FromResult(responseFactory(request));
        }
    }
}
