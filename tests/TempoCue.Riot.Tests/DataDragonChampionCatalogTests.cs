using System.Net;
using System.Text;
using TempoCue.Application;

namespace TempoCue.Riot.Tests;

public sealed class DataDragonChampionCatalogTests
{
    [Fact]
    public async Task LoadAsync_LoadsLocalizedChampionsAndUsesFreshCacheWithoutNetwork()
    {
        var cacheDirectory = CreateTestCacheDirectory();

        try
        {
            var requestedPaths = new List<string>();
            using var onlineHttpClient = CreateHttpClient(request =>
            {
                var requestPath = request.RequestUri?.AbsolutePath;
                requestedPaths.Add(requestPath ?? string.Empty);
                var json = requestPath switch
                {
                    "/api/versions.json" => "[\"26.15.1\"]",
                    "/cdn/26.15.1/data/ru_RU/champion.json" => ChampionJson,
                    _ => throw new InvalidOperationException(
                        $"Неожиданный Data Dragon endpoint: {request.RequestUri}")
                };

                return JsonResponse(json);
            });
            using var onlineDataDragonClient = new DataDragonClient(onlineHttpClient);
            var onlineCatalog = new DataDragonChampionCatalog(
                onlineDataDragonClient,
                cacheDirectory);

            var onlineResult = await onlineCatalog.LoadAsync("ru-RU");
            var repeatedOnlineResult = await onlineCatalog.LoadAsync("ru-RU");

            Assert.True(onlineResult.IsAvailable);
            Assert.Equal(ChampionCatalogSource.Network, onlineResult.Source);
            Assert.Equal(ChampionCatalogSource.Network, repeatedOnlineResult.Source);
            Assert.Single(requestedPaths, path => path == "/api/versions.json");
            Assert.Single(
                requestedPaths,
                path => path == "/cdn/26.15.1/data/ru_RU/champion.json");
            Assert.Same(onlineResult, repeatedOnlineResult);
            Assert.Equal(["Aatrox", "Urgot"], onlineResult.Champions.Select(x => x.Id));
            Assert.Equal("Ургот", onlineResult.Champions[1].DisplayName);
            Assert.Equal(6, onlineResult.Champions[1].NumericId);
            Assert.Equal(
                "https://ddragon.leagueoflegends.com/cdn/26.15.1/img/champion/Urgot.png",
                onlineResult.Champions[1].ImageUri.AbsoluteUri);

            using var offlineHttpClient = CreateHttpClient(_ =>
                throw new InvalidOperationException("Свежий кеш должен применяться до сети."));
            using var offlineDataDragonClient = new DataDragonClient(offlineHttpClient);
            var offlineCatalog = new DataDragonChampionCatalog(
                offlineDataDragonClient,
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
    public async Task LoadAsync_RefreshesStaleCacheAndFallsBackWhenNetworkIsUnavailable()
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
                    _ => throw new InvalidOperationException()
                };
                return JsonResponse(json);
            });
            using var onlineDataDragonClient = new DataDragonClient(onlineHttpClient);
            var onlineCatalog = new DataDragonChampionCatalog(
                onlineDataDragonClient,
                cacheDirectory);
            await onlineCatalog.LoadAsync("ru-RU");

            var cachePath = Path.Combine(cacheDirectory, "champions-ru_RU.json");
            var cacheJson = await File.ReadAllTextAsync(cachePath);
            using var cacheDocument = System.Text.Json.JsonDocument.Parse(cacheJson);
            var savedAtValue = cacheDocument.RootElement
                .GetProperty("savedAtUtc")
                .GetString();
            Assert.NotNull(savedAtValue);
            var staleSavedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(2));
            var staleJson = cacheJson.Replace(
                savedAtValue,
                staleSavedAt.ToString("O"),
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(cachePath, staleJson);

            var networkRequests = 0;
            using var offlineHttpClient = CreateHttpClient(_ =>
            {
                networkRequests++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });
            using var offlineDataDragonClient = new DataDragonClient(offlineHttpClient);
            var offlineCatalog = new DataDragonChampionCatalog(
                offlineDataDragonClient,
                cacheDirectory);

            var result = await offlineCatalog.LoadAsync("ru-RU");

            Assert.True(result.IsAvailable);
            Assert.Equal(ChampionCatalogSource.Cache, result.Source);
            Assert.True(networkRequests > 0);
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
            using var dataDragonClient = new DataDragonClient(httpClient);
            var catalog = new DataDragonChampionCatalog(dataDragonClient, cacheDirectory);

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
        new(new StubHttpMessageHandler(responseFactory));

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
              "key": "6",
              "name": "Ургот",
              "image": { "full": "Urgot.png" }
            },
            "Aatrox": {
              "id": "Aatrox",
              "key": "266",
              "name": "Атрокс",
              "image": { "full": "Aatrox.png" }
            },
            "Jade_Urgot": {
              "id": "Jade_Urgot",
              "key": "6",
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
