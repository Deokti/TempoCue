using System.Net.Http.Json;
using System.Text.Json;

namespace TempoCue.Riot;

/// <summary>
/// Выполняет общие запросы к Riot Data Dragon и предоставляет одну актуальную версию данных
/// всем специализированным каталогам текущего запуска приложения.
/// </summary>
public sealed class DataDragonClient : IDisposable
{
    private static readonly Uri BaseUri = new(
        "https://ddragon.leagueoflegends.com/",
        UriKind.Absolute);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _versionLock = new(1, 1);
    private IReadOnlyList<string>? _versions;

    public DataDragonClient()
        : this(CreateHttpClient(), ownsHttpClient: true)
    {
    }

    public DataDragonClient(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false)
    {
    }

    private DataDragonClient(HttpClient httpClient, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    internal async Task<IReadOnlyList<string>> GetVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_versions is not null)
        {
            return _versions;
        }

        await _versionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_versions is not null)
            {
                return _versions;
            }

            var versions = await _httpClient.GetFromJsonAsync<string[]>(
                    new Uri(BaseUri, "api/versions.json"),
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            _versions = versions?
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray()
                ?? [];
            return _versions;
        }
        finally
        {
            _versionLock.Release();
        }
    }

    internal async Task<string?> GetLatestVersionAsync(
        CancellationToken cancellationToken = default) =>
        (await GetVersionsAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();

    internal Task<T?> GetJsonAsync<T>(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return _httpClient.GetFromJsonAsync<T>(
            new Uri(BaseUri, relativePath),
            JsonOptions,
            cancellationToken);
    }

    internal static Uri CreateResourceUri(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return new Uri(BaseUri, relativePath);
    }

    internal static string NormalizeLocale(string languageCode) =>
        languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? "ru_RU"
            : "en_US";

    internal static bool IsExpectedExternalFailure(Exception exception) =>
        exception is HttpRequestException
            or JsonException
            or InvalidDataException
            or TaskCanceledException;

    public void Dispose()
    {
        // WPF не может синхронно дождаться всех async-обработчиков при OnExit. Семафор не
        // использует AvailableWaitHandle, поэтому его безопаснее собрать вместе с клиентом,
        // чем уничтожить до Release() ещё завершающегося запроса каталога.
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateHttpClient() => new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };
}
