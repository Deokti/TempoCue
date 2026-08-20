using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace TempoCue.Riot.Tests;

/// <summary>Возвращает управляемый HTTP-ответ без сети и запоминает URI запросов.</summary>
internal sealed class StubHttpMessageHandler(
    string responseJson,
    HttpStatusCode allGameStatusCode = HttpStatusCode.OK,
    Func<HttpContent>? contentFactory = null,
    IReadOnlyDictionary<Uri, string>? responseJsonByUri = null) : HttpMessageHandler
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
                ?? new StringContent(
                    responseJsonByUri is not null
                        && responseJsonByUri.TryGetValue(requestUri, out var routedJson)
                            ? routedJson
                            : responseJson,
                    Encoding.UTF8,
                    "application/json")
        };

        return Task.FromResult(response);
    }
}
