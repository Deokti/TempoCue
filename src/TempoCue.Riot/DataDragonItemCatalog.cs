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
            .Select(pair =>
            {
                var tags = pair.Value.Tags?
                    .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                    ?? [];
                return new ItemCatalogEntry(
                    int.Parse(pair.Key, CultureInfo.InvariantCulture),
                    pair.Value.Name!,
                    pair.Value.PlainText?.Trim() ?? string.Empty,
                    DataDragonClient.CreateResourceUri(
                        $"cdn/{Uri.EscapeDataString(version)}/img/item/{Uri.EscapeDataString(pair.Value.Image!.Full!)}"),
                    Math.Max(0, pair.Value.Gold?.Total ?? 0),
                    tags,
                    ResolveCategory(pair.Value, tags),
                    MapStats(pair.Value.Stats));
            })
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
                        Tags = item.Tags.ToList(),
                        Category = item.Category,
                        Stats = item.Stats
                            .Select(stat => new DataDragonCachedItemStatDto
                            {
                                Kind = stat.Kind,
                                Value = stat.Value
                            })
                            .ToList()
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
                    item.Tags?.ToArray() ?? [],
                    item.Category,
                    item.Stats?
                        .Where(static stat => stat.Value != 0)
                        .Select(static stat => new ItemStatEntry(stat.Kind, stat.Value))
                        .ToArray()
                        ?? []))
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

    private static ItemCatalogCategory ResolveCategory(
        DataDragonItemDto item,
        IReadOnlyList<string> tags)
    {
        if (tags.Contains("Boots", StringComparer.OrdinalIgnoreCase))
        {
            return ItemCatalogCategory.Boots;
        }

        var isStarter = (tags.Contains("Lane", StringComparer.OrdinalIgnoreCase)
                         || tags.Contains("Jungle", StringComparer.OrdinalIgnoreCase)
                         || tags.Contains("Consumable", StringComparer.OrdinalIgnoreCase))
                        && item.From is not { Count: > 0 }
                        && (item.Gold?.Total ?? 0) <= 500;
        if (isStarter)
        {
            return ItemCatalogCategory.Starter;
        }

        return item.Depth switch
        {
            >= 3 => ItemCatalogCategory.Legendary,
            2 => ItemCatalogCategory.Epic,
            _ => ItemCatalogCategory.Basic
        };
    }

    private static IReadOnlyList<ItemStatEntry> MapStats(
        IReadOnlyDictionary<string, double>? stats)
    {
        if (stats is null)
        {
            return [];
        }

        var result = new List<ItemStatEntry>();
        foreach (var stat in stats)
        {
            if (TryMapStat(stat.Key, stat.Value, out var mappedStat))
            {
                result.Add(mappedStat);
            }
        }

        return result
            .OrderBy(static stat => stat.Kind)
            .ToArray();
    }

    private static bool TryMapStat(
        string sourceName,
        double sourceValue,
        out ItemStatEntry mappedStat)
    {
        var mapping = sourceName switch
        {
            "FlatHPPoolMod" => (ItemStatKind.Health, 1d),
            "FlatMPPoolMod" => (ItemStatKind.Mana, 1d),
            "FlatPhysicalDamageMod" => (ItemStatKind.AttackDamage, 1d),
            "FlatMagicDamageMod" => (ItemStatKind.AbilityPower, 1d),
            "FlatArmorMod" => (ItemStatKind.Armor, 1d),
            "FlatSpellBlockMod" => (ItemStatKind.MagicResistance, 1d),
            "FlatMovementSpeedMod" => (ItemStatKind.MoveSpeed, 1d),
            "PercentMovementSpeedMod" => (ItemStatKind.MoveSpeedPercent, 100d),
            "PercentAttackSpeedMod" => (ItemStatKind.AttackSpeedPercent, 100d),
            "PercentLifeStealMod" => (ItemStatKind.LifeStealPercent, 100d),
            "FlatCritChanceMod" or "PercentCritChanceMod" =>
                (ItemStatKind.CriticalStrikeChancePercent, 100d),
            "FlatHPRegenMod" => (ItemStatKind.HealthRegen, 1d),
            "FlatMPRegenMod" => (ItemStatKind.ManaRegen, 1d),
            _ => ((ItemStatKind)(-1), 0d)
        };

        var value = sourceValue * mapping.Item2;
        if ((int)mapping.Item1 < 0 || Math.Abs(value) < double.Epsilon)
        {
            mappedStat = new ItemStatEntry(default, 0);
            return false;
        }

        mappedStat = new ItemStatEntry(mapping.Item1, value);
        return true;
    }

    private static bool IsExpectedCacheFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException;
}
