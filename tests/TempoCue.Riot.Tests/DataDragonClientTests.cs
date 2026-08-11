using System.Net;

namespace TempoCue.Riot.Tests;

public sealed class DataDragonClientTests
{
    [Fact]
    public async Task Dispose_DoesNotBreakVersionRequestAlreadyInProgress()
    {
        var handler = new DeferredResponseHandler();
        using var httpClient = new HttpClient(handler);
        var client = new DataDragonClient(httpClient);
        var cacheDirectory = Path.Combine(
            Path.GetTempPath(),
            "TempoCue.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDirectory);

        try
        {
            var catalog = new DataDragonGamePatchCatalog(client, cacheDirectory);
            var load = catalog.LoadAsync();
            await handler.RequestStarted;

            client.Dispose();
            handler.Complete("[\"26.15.1\"]");

            Assert.Equal("26.15", Assert.Single((await load).Patches).Patch);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    private sealed class DeferredResponseHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _requestStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<HttpResponseMessage> _response =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestStarted => _requestStarted.Task;

        public void Complete(string json) =>
            _response.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requestStarted.TrySetResult();
            return _response.Task.WaitAsync(cancellationToken);
        }
    }
}
