using System.Globalization;
using System.Text.Json;
using TempoCue.Application;

namespace TempoCue.Riot;

/// <summary>
/// Загружает доступные на Ущелье призывателей предметы для выбранного патча
/// и использует последний успешный локальный кеш при недоступности сети.
/// </summary>
public sealed class DataDragonItemCatalog : IItemCatalog
{
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DataDragonClient _dataDragonClient;
    private readonly string _cacheDirectory;

    public DataDragonItemCatalog(
        DataDragonClient dataDragonClient,
        string cacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(dataDragonClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _dataDragonClient = dataDragonClient;
        _cacheDirectory = cacheDirectory;
    }

    public async Task<ItemCatalogLoadResult> LoadAsync(
        string gamePatch,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gamePatch);
        var normalizedPatch = gamePatch.Trim();
        var locale = DataDragonClient.NormalizeLocale(languageCode);

        try
        {
            var versions = await _dataDragonClient.GetVersionsAsync(cancellationToken);
            var version = FindVersion(versions, normalizedPatch);
            if (version is null)
            {
                return await LoadCacheAsync(normalizedPatch, locale, cancellationToken);
            }

            var escapedVersion = Uri.EscapeDataString(version);
            var response = await _dataDragonClient.GetJsonAsync<DataDragonItemListDto>(
                $"cdn/{escapedVersion}/data/{locale}/item.json",
                cancellationToken);
            var items = MapItems(response, version, locale);
            if (items.Count == 0)
            {
                return await LoadCacheAsync(normalizedPatch, locale, cancellationToken);
            }

            var cacheUpdateFailed = !await TrySaveCacheAsync(
                version,
                normalizedPatch,
                locale,
                items,
                cancellationToken);
            return new ItemCatalogLoadResult(
                items,
                ItemCatalogSource.Network,
                cacheUpdateFailed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (DataDragonClient.IsExpectedExternalFailure(exception))
        {
            return await LoadCacheAsync(normalizedPatch, locale, cancellationToken);
        }
    }

    private static string? FindVersion(
        IReadOnlyList<string> versions,
        string gamePatch) =>
        versions.FirstOrDefault(version =>
            string.Equals(version, gamePatch, StringComparison.OrdinalIgnoreCase)
            || version.StartsWith($"{gamePatch}.", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ItemCatalogEntry> MapItems(
        DataDragonItemListDto? response,
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

        return response.Data
            .Where(static pair =>
                int.TryParse(pair.Key, out _)
                && !string.IsNullOrWhiteSpace(pair.Value.Name)
                && !string.IsNullOrWhiteSpace(pair.Value.Image?.Full)
                && pair.Value.Gold?.Purchasable == true
                && pair.Value.InStore is not false
                && pair.Value.Maps?.GetValueOrDefault("11") == true)
            .Select(pair => new ItemCatalogEntry(
                int.Parse(pair.Key, CultureInfo.InvariantCulture),
                pair.Value.Name!,
                pair.Value.PlainText?.Trim() ?? string.Empty,
                DataDragonClient.CreateResourceUri(
                    $"cdn/{Uri.EscapeDataString(version)}/img/item/{Uri.EscapeDataString(pair.Value.Image!.Full!)}"),
                Math.Max(0, pair.Value.Gold?.Total ?? 0),
                pair.Value.Tags?
                    .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                    ?? []))
            .OrderBy(item => item.DisplayName, StringComparer.Create(culture, true))
            .ThenBy(item => item.Id)
            .ToArray();
    }

    private async Task<bool> TrySaveCacheAsync(
        string version,
        string gamePatch,
        string locale,
        IReadOnlyList<ItemCatalogEntry> items,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var cache = new DataDragonItemCacheDto
            {
                Version = version,
                GamePatch = gamePatch,
                Locale = locale,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Items = items
                    .Select(item => new DataDragonCachedItemDto
                    {
                        Id = item.Id,
                        DisplayName = item.DisplayName,
                        Description = item.Description,
                        ImageUrl = item.ImageUri.AbsoluteUri,
                        Price = item.Price,
                        Tags = item.Tags.ToList()
                    })
                    .ToList()
            };
            var cachePath = GetCachePath(gamePatch, locale);
            var temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
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
            return false;
        }
    }

    private async Task<ItemCatalogLoadResult> LoadCacheAsync(
        string gamePatch,
        string locale,
        CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = GetCachePath(gamePatch, locale);
            if (!File.Exists(cachePath))
            {
                return ItemCatalogLoadResult.Unavailable();
            }

            await using var stream = File.OpenRead(cachePath);
            var cache = await JsonSerializer.DeserializeAsync<DataDragonItemCacheDto>(
                stream,
                CacheJsonOptions,
                cancellationToken);
            if (cache?.GamePatch != gamePatch
                || cache.Locale != locale
                || cache.Items is null)
            {
                return ItemCatalogLoadResult.Unavailable();
            }

            var items = cache.Items
                .Where(static item =>
                    item.Id > 0
                    && !string.IsNullOrWhiteSpace(item.DisplayName)
                    && Uri.TryCreate(item.ImageUrl, UriKind.Absolute, out _))
                .Select(item => new ItemCatalogEntry(
                    item.Id,
                    item.DisplayName!,
                    item.Description ?? string.Empty,
                    new Uri(item.ImageUrl!, UriKind.Absolute),
                    Math.Max(0, item.Price),
                    item.Tags?.ToArray() ?? []))
                .ToArray();

            return items.Length == 0
                ? ItemCatalogLoadResult.Unavailable()
                : new ItemCatalogLoadResult(items, ItemCatalogSource.Cache);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedCacheFailure(exception))
        {
            return ItemCatalogLoadResult.Unavailable();
        }
    }

    private string GetCachePath(string gamePatch, string locale)
    {
        var patchKey = string.Concat(gamePatch.Where(static character =>
            char.IsLetterOrDigit(character) || character is '.' or '-'));
        return Path.Combine(_cacheDirectory, $"items-{patchKey}-{locale}.json");
    }

    private static bool IsExpectedCacheFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException;
}
