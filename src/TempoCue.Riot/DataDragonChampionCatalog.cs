using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using TempoCue.Application;

namespace TempoCue.Riot;

/// <summary>
/// Загружает локализованный список чемпионов из Riot Data Dragon и использует
/// последний успешный ответ при временной недоступности сети.
/// </summary>
public sealed class DataDragonChampionCatalog : IChampionCatalog, IDisposable
{
    private static readonly Uri DataDragonBaseUri = new(
        "https://ddragon.leagueoflegends.com/",
        UriKind.Absolute);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _cacheDirectory;

    public DataDragonChampionCatalog(string cacheDirectory)
        : this(CreateHttpClient(), cacheDirectory, ownsHttpClient: true)
    {
    }

    public DataDragonChampionCatalog(HttpClient httpClient, string cacheDirectory)
        : this(httpClient, cacheDirectory, ownsHttpClient: false)
    {
    }

    private DataDragonChampionCatalog(
        HttpClient httpClient,
        string cacheDirectory,
        bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _httpClient = httpClient;
        _cacheDirectory = cacheDirectory;
        _ownsHttpClient = ownsHttpClient;
    }

    public async Task<ChampionCatalogLoadResult> LoadAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var locale = NormalizeLocale(languageCode);

        try
        {
            var versions = await _httpClient.GetFromJsonAsync<string[]>(
                "api/versions.json",
                JsonOptions,
                cancellationToken);
            var version = versions?.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
            if (version is null)
            {
                return await LoadCacheAsync(locale, cancellationToken);
            }

            var response = await _httpClient.GetFromJsonAsync<DataDragonChampionListDto>(
                $"cdn/{version}/data/{locale}/champion.json",
                JsonOptions,
                cancellationToken);
            var champions = MapChampions(response, version, locale);
            if (champions.Count == 0)
            {
                return await LoadCacheAsync(locale, cancellationToken);
            }

            var cacheUpdateFailed = !await TrySaveCacheAsync(
                version,
                locale,
                champions,
                cancellationToken);
            return new ChampionCatalogLoadResult(
                champions,
                ChampionCatalogSource.Network,
                cacheUpdateFailed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedExternalFailure(exception))
        {
            return await LoadCacheAsync(locale, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    internal static string NormalizeLocale(string languageCode) =>
        languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? "ru_RU"
            : "en_US";

    private static HttpClient CreateHttpClient() => new()
    {
        BaseAddress = DataDragonBaseUri,
        Timeout = TimeSpan.FromSeconds(12)
    };

    private static IReadOnlyList<ChampionCatalogEntry> MapChampions(
        DataDragonChampionListDto? response,
        string version,
        string locale)
    {
        if (response?.Data is null)
        {
            return [];
        }

        var culture = locale == "ru_RU"
            ? CultureInfo.GetCultureInfo("ru-RU")
            : CultureInfo.GetCultureInfo("en-US");

        return response.Data.Values
            .Where(static champion =>
                !string.IsNullOrWhiteSpace(champion.Id)
                && IsSummonersRiftChampion(champion.Id)
                && !string.IsNullOrWhiteSpace(champion.Name)
                && !string.IsNullOrWhiteSpace(champion.Image?.Full))
            .Select(champion => new ChampionCatalogEntry(
                champion.Id!,
                champion.Name!,
                new Uri(
                    DataDragonBaseUri,
                    $"cdn/{version}/img/champion/{Uri.EscapeDataString(champion.Image!.Full!)}")))
            .OrderBy(champion => champion.DisplayName, StringComparer.Create(culture, true))
            .ToArray();
    }

    private async Task<bool> TrySaveCacheAsync(
        string version,
        string locale,
        IReadOnlyList<ChampionCatalogEntry> champions,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var cache = new DataDragonChampionCacheDto
            {
                Version = version,
                Locale = locale,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Champions = champions
                    .Select(champion => new DataDragonCachedChampionDto
                    {
                        Id = champion.Id,
                        DisplayName = champion.DisplayName,
                        ImageUrl = champion.ImageUri.AbsoluteUri
                    })
                    .ToList()
            };
            var cachePath = GetCachePath(locale);
            var temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    cache,
                    JsonOptions,
                    cancellationToken);
            }

            File.Move(temporaryPath, cachePath, overwrite: true);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedCacheFailure(exception))
        {
            // Ошибка преобразуется в явный признак результата: сетевые данные остаются
            // пригодными для текущего запуска, но вызывающая сторона знает о сбое кеша.
            return false;
        }
    }

    private async Task<ChampionCatalogLoadResult> LoadCacheAsync(
        string locale,
        CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = GetCachePath(locale);
            if (!File.Exists(cachePath))
            {
                return ChampionCatalogLoadResult.Unavailable();
            }

            await using var stream = File.OpenRead(cachePath);
            var cache = await JsonSerializer.DeserializeAsync<DataDragonChampionCacheDto>(
                stream,
                JsonOptions,
                cancellationToken);
            if (cache?.Locale != locale || cache.Champions is null)
            {
                return ChampionCatalogLoadResult.Unavailable();
            }

            var champions = cache.Champions
                .Where(static champion =>
                    !string.IsNullOrWhiteSpace(champion.Id)
                    && IsSummonersRiftChampion(champion.Id)
                    && !string.IsNullOrWhiteSpace(champion.DisplayName)
                    && Uri.TryCreate(champion.ImageUrl, UriKind.Absolute, out _))
                .Select(champion => new ChampionCatalogEntry(
                    champion.Id!,
                    champion.DisplayName!,
                    new Uri(champion.ImageUrl!, UriKind.Absolute)))
                .ToArray();

            return champions.Length == 0
                ? ChampionCatalogLoadResult.Unavailable()
                : new ChampionCatalogLoadResult(champions, ChampionCatalogSource.Cache);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedCacheFailure(exception))
        {
            return ChampionCatalogLoadResult.Unavailable();
        }
    }

    private string GetCachePath(string locale) =>
        Path.Combine(_cacheDirectory, $"champions-{locale}.json");

    private static bool IsSummonersRiftChampion(string championId) =>
        !championId.StartsWith("Jade_", StringComparison.OrdinalIgnoreCase);

    private static bool IsExpectedExternalFailure(Exception exception) =>
        exception is HttpRequestException
            or JsonException
            or InvalidDataException
            or TaskCanceledException;

    private static bool IsExpectedCacheFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException;
}
