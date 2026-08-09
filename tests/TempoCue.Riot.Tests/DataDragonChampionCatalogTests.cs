using System.Net;
using System.Text;
using TempoCue.Application;

namespace TempoCue.Riot.Tests;

public sealed class DataDragonChampionCatalogTests
{
    [Fact]
    public async Task LoadAsync_LoadsLocalizedChampionsAndFallsBackToCache()
    {
        var cacheDirectory = CreateTestCacheDirectory();

        try
        {
            using var onlineHttpClient = CreateHttpClient(request =>
            {
                var json = request.RequestUri?.AbsolutePath switch
                {
                    "/api/versions.json" => "[\"26.15.1\"]",
                    "/cdn/26.15.1/data/ru_RU/champion.json" => ChampionJson,
                    _ => throw new InvalidOperationException(
                        $"Неожиданный Data Dragon endpoint: {request.RequestUri}")
                };

                return JsonResponse(json);
            });
            using var onlineCatalog = new DataDragonChampionCatalog(
                onlineHttpClient,
                cacheDirectory);

            var onlineResult = await onlineCatalog.LoadAsync("ru-RU");

            Assert.True(onlineResult.IsAvailable);
            Assert.Equal(ChampionCatalogSource.Network, onlineResult.Source);
            Assert.Equal(["Aatrox", "Urgot"], onlineResult.Champions.Select(x => x.Id));
            Assert.Equal("Ургот", onlineResult.Champions[1].DisplayName);
            Assert.Equal(
                "https://ddragon.leagueoflegends.com/cdn/26.15.1/img/champion/Urgot.png",
                onlineResult.Champions[1].ImageUri.AbsoluteUri);

            using var offlineHttpClient = CreateHttpClient(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using var offlineCatalog = new DataDragonChampionCatalog(
                offlineHttpClient,
                cacheDirectory);

            var cachedResult = await offlineCatalog.LoadAsync("ru-RU");

            Assert.True(cachedResult.IsAvailable);
            Assert.Equal(ChampionCatalogSource.Cache, cachedResult.Source);
            Assert.Equal(onlineResult.Champions, cachedResult.Champions);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsUnavailableWhenNetworkAndCacheAreMissing()
    {
        var cacheDirectory = CreateTestCacheDirectory();

        try
        {
            using var httpClient = CreateHttpClient(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using var catalog = new DataDragonChampionCatalog(httpClient, cacheDirectory);

            var result = await catalog.LoadAsync("en-US");

            Assert.False(result.IsAvailable);
            Assert.Equal(ChampionCatalogSource.Unavailable, result.Source);
            Assert.Empty(result.Champions);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://ddragon.leagueoflegends.com/")
        };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string CreateTestCacheDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "TempoCue.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private const string ChampionJson = """
        {
          "data": {
            "Urgot": {
              "id": "Urgot",
              "name": "Ургот",
              "image": { "full": "Urgot.png" }
            },
            "Aatrox": {
              "id": "Aatrox",
              "name": "Атрокс",
              "image": { "full": "Aatrox.png" }
            },
            "Jade_Urgot": {
              "id": "Jade_Urgot",
              "name": "Ургот",
              "image": { "full": "Jade_Urgot.png" }
            }
          }
        }
        """;

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
