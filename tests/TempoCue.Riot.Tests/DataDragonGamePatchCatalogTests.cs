using System.Net;
using System.Text;

namespace TempoCue.Riot.Tests;

public sealed class DataDragonGamePatchCatalogTests
{
    [Fact]
    public async Task LoadAsync_NormalizesVersionsAndMarksOnlyCurrentPatch()
    {
        var cacheDirectory = CreateTestCacheDirectory();

        try
        {
            using var httpClient = CreateHttpClient(_ => JsonResponse(
                "[\"26.15.1\",\"26.14.1\",\"26.14.0\",\"25.24.1\",\"invalid\"]"));
            using var dataDragonClient = new DataDragonClient(httpClient);
            var catalog = new DataDragonGamePatchCatalog(dataDragonClient, cacheDirectory);

            var result = await catalog.LoadAsync();

            Assert.True(result.IsAvailable);
            Assert.False(result.IsFromCache);
            Assert.Equal(["26.15", "26.14", "25.24"], result.Patches.Select(x => x.Patch));
            Assert.True(result.Patches[0].IsCurrent);
            Assert.All(result.Patches.Skip(1), patch => Assert.False(patch.IsCurrent));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_UsesCacheWithoutCallingOldPatchCurrent()
    {
        var cacheDirectory = CreateTestCacheDirectory();

        try
        {
            using (var onlineHttpClient = CreateHttpClient(_ =>
                       JsonResponse("[\"26.15.1\",\"26.14.1\"]")))
            using (var onlineDataDragonClient = new DataDragonClient(onlineHttpClient))
            {
                var onlineCatalog = new DataDragonGamePatchCatalog(
                    onlineDataDragonClient,
                    cacheDirectory);
                await onlineCatalog.LoadAsync();
            }

            using var offlineHttpClient = CreateHttpClient(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using var offlineDataDragonClient = new DataDragonClient(offlineHttpClient);
            var offlineCatalog = new DataDragonGamePatchCatalog(
                offlineDataDragonClient,
                cacheDirectory);

            var result = await offlineCatalog.LoadAsync();

            Assert.True(result.IsAvailable);
            Assert.True(result.IsFromCache);
            Assert.Equal(["26.15", "26.14"], result.Patches.Select(x => x.Patch));
            Assert.All(result.Patches, patch => Assert.False(patch.IsCurrent));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsUnavailableWithoutNetworkOrCache()
    {
        var cacheDirectory = CreateTestCacheDirectory();

        try
        {
            using var httpClient = CreateHttpClient(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using var dataDragonClient = new DataDragonClient(httpClient);
            var catalog = new DataDragonGamePatchCatalog(dataDragonClient, cacheDirectory);

            var result = await catalog.LoadAsync();

            Assert.False(result.IsAvailable);
            Assert.Empty(result.Patches);
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

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
