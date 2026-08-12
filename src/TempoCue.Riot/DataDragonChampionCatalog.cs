using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using TempoCue.Application;

namespace TempoCue.Riot;

/// <summary>
/// Загружает локализованный список чемпионов из Riot Data Dragon и использует
/// последний успешный ответ при временной недоступности сети.
/// </summary>
public sealed class DataDragonChampionCatalog : IChampionCatalog
{
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DataDragonClient _dataDragonClient;
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ChampionCatalogLoadResult> _memoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    public DataDragonChampionCatalog(
        DataDragonClient dataDragonClient,
        string cacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(dataDragonClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _dataDragonClient = dataDragonClient;
        _cacheDirectory = cacheDirectory;
    }

    public async Task<ChampionCatalogLoadResult> LoadAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var locale = DataDragonClient.NormalizeLocale(languageCode);
        if (_memoryCache.TryGetValue(locale, out var cachedResult))
        {
            return cachedResult;
        }

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_memoryCache.TryGetValue(locale, out cachedResult))
            {
                return cachedResult;
            }

            var result = await LoadCoreAsync(locale, cancellationToken).ConfigureAwait(false);
            if (result.IsAvailable)
            {
                _memoryCache[locale] = result;
            }

            return result;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task<ChampionCatalogLoadResult> LoadCoreAsync(
        string locale,
        CancellationToken cancellationToken)
    {

        try
        {
            var version = await _dataDragonClient.GetLatestVersionAsync(cancellationToken);
            if (version is null)
            {
                return await LoadCacheAsync(locale, cancellationToken);
            }

            var escapedVersion = Uri.EscapeDataString(version);
            var response = await _dataDragonClient.GetJsonAsync<DataDragonChampionListDto>(
                $"cdn/{escapedVersion}/data/{locale}/champion.json",
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
        catch (Exception exception) when (DataDragonClient.IsExpectedExternalFailure(exception))
        {
            return await LoadCacheAsync(locale, cancellationToken);
        }
    }

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
                && int.TryParse(champion.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var numericId)
                && numericId > 0
                && !string.IsNullOrWhiteSpace(champion.Name)
                && !string.IsNullOrWhiteSpace(champion.Image?.Full))
            .Select(champion => new ChampionCatalogEntry(
                champion.Id!,
                int.Parse(champion.Key!, CultureInfo.InvariantCulture),
                champion.Name!,
                DataDragonClient.CreateResourceUri(
                    $"cdn/{Uri.EscapeDataString(version)}/img/champion/{Uri.EscapeDataString(champion.Image!.Full!)}")))
            .OrderBy(champion => champion.DisplayName, StringComparer.Create(culture, true))
            .ToArray();
    }

    private async Task<bool> TrySaveCacheAsync(
        string version,
        string locale,
        IReadOnlyList<ChampionCatalogEntry> champions,
        CancellationToken cancellationToken)
    {
        string? temporaryPath = null;
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
                        NumericId = champion.NumericId,
                        DisplayName = champion.DisplayName,
                        ImageUrl = champion.ImageUri.AbsoluteUri
                    })
                    .ToList()
            };
            var cachePath = GetCachePath(locale);
            temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    cache,
                    CacheJsonOptions,
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
        finally
        {
            DeleteTemporaryFile(temporaryPath);
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
                CacheJsonOptions,
                cancellationToken);
            if (cache?.Locale != locale || cache.Champions is null)
            {
                return ChampionCatalogLoadResult.Unavailable();
            }

            var champions = cache.Champions
                .Where(static champion =>
                    !string.IsNullOrWhiteSpace(champion.Id)
                    && IsSummonersRiftChampion(champion.Id)
                    && champion.NumericId > 0
                    && !string.IsNullOrWhiteSpace(champion.DisplayName)
                    && Uri.TryCreate(champion.ImageUrl, UriKind.Absolute, out _))
                .Select(champion => new ChampionCatalogEntry(
                    champion.Id!,
                    champion.NumericId,
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

    private static void DeleteTemporaryFile(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (IsExpectedCacheFailure(exception))
        {
            // Ошибка очистки временного кеша не должна скрывать исходный результат загрузки.
        }
    }

    private static bool IsExpectedCacheFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException;
}
