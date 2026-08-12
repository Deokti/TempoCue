using System.Net;
using System.Text;
using TempoCue.Application;

namespace TempoCue.Riot.Tests;

public sealed class DataDragonItemCatalogTests
{
    [Fact]
    public async Task LoadAsync_LoadsPatchItemsAndFallsBackToLocalizedCache()
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
                    "/api/versions.json" => "[\"26.16.1\",\"26.15.1\"]",
                    "/cdn/26.15.1/data/ru_RU/item.json" => ItemJson,
                    _ => throw new InvalidOperationException(
                        $"Неожиданный Data Dragon endpoint: {request.RequestUri}")
                };

                return JsonResponse(json);
            });
            using var onlineDataDragonClient = new DataDragonClient(onlineHttpClient);
            var onlineCatalog = new DataDragonItemCatalog(
                onlineDataDragonClient,
                cacheDirectory);

            var onlineResult = await onlineCatalog.LoadAsync("26.15", "ru-RU");

            Assert.True(onlineResult.IsAvailable);
            Assert.Equal(ItemCatalogSource.Network, onlineResult.Source);
            Assert.Contains("/cdn/26.15.1/data/ru_RU/item.json", requestedPaths);
            var cleaver = Assert.Single(onlineResult.Items);
            Assert.Equal(3071, cleaver.Id);
            Assert.Equal("Чёрная секира", cleaver.DisplayName);
            Assert.Equal(3000, cleaver.Price);
            Assert.Contains("Damage", cleaver.Tags);
            Assert.Equal(ItemCatalogCategory.Legendary, cleaver.Category);
            Assert.Contains(
                cleaver.Stats,
                stat => stat.Kind == ItemStatKind.Health && stat.Value == 400);
            Assert.Contains(
                cleaver.Stats,
                stat => stat.Kind == ItemStatKind.AttackDamage && stat.Value == 40);
            Assert.Equal(
                "https://ddragon.leagueoflegends.com/cdn/26.15.1/img/item/3071.png",
                cleaver.ImageUri.AbsoluteUri);

            using var offlineHttpClient = CreateHttpClient(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using var offlineDataDragonClient = new DataDragonClient(offlineHttpClient);
            var offlineCatalog = new DataDragonItemCatalog(
                offlineDataDragonClient,
                cacheDirectory);

            var cachedResult = await offlineCatalog.LoadAsync("26.15", "ru-RU");

            Assert.True(cachedResult.IsAvailable);
            Assert.Equal(ItemCatalogSource.Cache, cachedResult.Source);
            Assert.Collection(
                cachedResult.Items,
                item =>
                {
                    Assert.Equal(3071, item.Id);
                    Assert.Equal("Чёрная секира", item.DisplayName);
                    Assert.Equal("Скорость и пробивание брони", item.Description);
                    Assert.Equal(3000, item.Price);
                    Assert.Equal(["Damage", "CooldownReduction"], item.Tags);
                    Assert.Equal(ItemCatalogCategory.Legendary, item.Category);
                    Assert.Equal(
                        [
                            new ItemStatEntry(ItemStatKind.Health, 400),
                            new ItemStatEntry(ItemStatKind.AttackDamage, 40)
                        ],
                        item.Stats);
                });
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_DoesNotUseCacheFromAnotherPatch()
    {
        var cacheDirectory = CreateTestCacheDirectory();

        try
        {
            using var httpClient = CreateHttpClient(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using var dataDragonClient = new DataDragonClient(httpClient);
            var catalog = new DataDragonItemCatalog(dataDragonClient, cacheDirectory);

            var result = await catalog.LoadAsync("26.14", "en-US");

            Assert.False(result.IsAvailable);
            Assert.Equal(ItemCatalogSource.Unavailable, result.Source);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ClassifiesItemsFromTagsAndBuildDepth()
    {
        var cacheDirectory = CreateTestCacheDirectory();

        try
        {
            using var httpClient = CreateHttpClient(request => JsonResponse(
                request.RequestUri?.AbsolutePath == "/api/versions.json"
                    ? "[\"26.15.1\"]"
                    : CategoryItemJson));
            using var dataDragonClient = new DataDragonClient(httpClient);
            var catalog = new DataDragonItemCatalog(dataDragonClient, cacheDirectory);

            var result = await catalog.LoadAsync("26.15", "en-US");
            var items = result.Items.ToDictionary(item => item.Id);

            Assert.Equal(ItemCatalogCategory.Starter, items[1055].Category);
            Assert.Equal(ItemCatalogCategory.Boots, items[3006].Category);
            Assert.Equal(ItemCatalogCategory.Epic, items[3133].Category);
            Assert.Equal(ItemCatalogCategory.Basic, items[1036].Category);
            Assert.Contains(
                items[3006].Stats,
                stat => stat.Kind == ItemStatKind.AttackSpeedPercent && stat.Value == 25);
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

    private const string ItemJson = """
        {
          "data": {
            "3071": {
              "name": "Чёрная секира",
              "plaintext": "Скорость и пробивание брони",
              "image": { "full": "3071.png" },
              "gold": { "purchasable": true, "total": 3000 },
              "tags": ["Damage", "CooldownReduction"],
              "depth": 3,
              "from": ["1036", "2021", "3067"],
              "stats": {
                "FlatHPPoolMod": 400,
                "FlatPhysicalDamageMod": 40,
                "UnsupportedStat": 25
              },
              "maps": { "11": true }
            },
            "9998": {
              "name": "Предмет другой карты",
              "plaintext": "Не должен попасть в каталог",
              "image": { "full": "9998.png" },
              "gold": { "purchasable": true, "total": 1000 },
              "tags": ["Damage"],
              "maps": { "11": false }
            },
            "9999": {
              "name": "Скрытый предмет",
              "plaintext": "Не должен попасть в каталог",
              "image": { "full": "9999.png" },
              "gold": { "purchasable": false, "total": 0 },
              "tags": [],
              "maps": { "11": true }
            }
          }
        }
        """;

    private const string CategoryItemJson = """
        {
          "data": {
            "1055": {
              "name": "Doran's Blade", "plaintext": "Starter item", "depth": 1,
              "image": { "full": "1055.png" },
              "gold": { "purchasable": true, "total": 450 },
              "tags": ["Damage", "Lane"], "stats": {}, "maps": { "11": true }
            },
            "3006": {
              "name": "Berserker's Greaves", "plaintext": "Attack speed boots", "depth": 2,
              "from": ["1001", "1042"], "image": { "full": "3006.png" },
              "gold": { "purchasable": true, "total": 1100 },
              "tags": ["Boots", "AttackSpeed"],
              "stats": { "PercentAttackSpeedMod": 0.25, "FlatMovementSpeedMod": 45 },
              "maps": { "11": true }
            },
            "3133": {
              "name": "Caulfield's Warhammer", "plaintext": "Epic item", "depth": 2,
              "from": ["1036", "1036"], "image": { "full": "3133.png" },
              "gold": { "purchasable": true, "total": 1050 },
              "tags": ["Damage"], "stats": {}, "maps": { "11": true }
            },
            "1036": {
              "name": "Long Sword", "plaintext": "Basic item", "depth": 1,
              "image": { "full": "1036.png" },
              "gold": { "purchasable": true, "total": 350 },
              "tags": ["Damage"], "stats": {}, "maps": { "11": true }
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
