using System.Text.Json;
using TempoCue.Application;

namespace TempoCue.Riot;

/// <summary>
/// Преобразует версии Data Dragon в игровые патчи и сохраняет последний успешный список.
/// </summary>
public sealed class DataDragonGamePatchCatalog : IGamePatchCatalog
{
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DataDragonClient _dataDragonClient;
    private readonly string _cacheDirectory;

    public DataDragonGamePatchCatalog(
        DataDragonClient dataDragonClient,
        string cacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(dataDragonClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _dataDragonClient = dataDragonClient;
        _cacheDirectory = cacheDirectory;
    }

    public async Task<GamePatchCatalogLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await _dataDragonClient.GetVersionsAsync(cancellationToken);
            var patches = MapVersions(versions, markCurrent: true);
            if (patches.Count == 0)
            {
                return await LoadCacheAsync(cancellationToken);
            }

            await TrySaveCacheAsync(patches, cancellationToken);
            return new GamePatchCatalogLoadResult(patches);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (DataDragonClient.IsExpectedExternalFailure(exception))
        {
            return await LoadCacheAsync(cancellationToken);
        }
    }

    internal static IReadOnlyList<GamePatchCatalogEntry> MapVersions(
        IEnumerable<string> versions,
        bool markCurrent)
    {
        ArgumentNullException.ThrowIfNull(versions);

        var patches = new List<GamePatchCatalogEntry>();
        var uniquePatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in versions)
        {
            var patch = NormalizePatch(version);
            if (patch is null || !uniquePatches.Add(patch))
            {
                continue;
            }

            patches.Add(new GamePatchCatalogEntry(
                patch,
                markCurrent && patches.Count == 0));
        }

        return patches;
    }

    private async Task TrySaveCacheAsync(
        IReadOnlyList<GamePatchCatalogEntry> patches,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var cachePath = GetCachePath();
            var temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
            var cache = new DataDragonGamePatchCacheDto
            {
                SavedAtUtc = DateTimeOffset.UtcNow,
                Patches = patches.Select(static patch => patch.Patch).ToList()
            };
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    cache,
                    CacheJsonOptions,
                    cancellationToken);
            }

            File.Move(temporaryPath, cachePath, overwrite: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedCacheFailure(exception))
        {
            // Список из сети пригоден для текущего запуска, даже если обновить кеш не удалось.
        }
    }

    private async Task<GamePatchCatalogLoadResult> LoadCacheAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = GetCachePath();
            if (!File.Exists(cachePath))
            {
                return GamePatchCatalogLoadResult.Unavailable();
            }

            await using var stream = File.OpenRead(cachePath);
            var cache = await JsonSerializer.DeserializeAsync<DataDragonGamePatchCacheDto>(
                stream,
                CacheJsonOptions,
                cancellationToken);
            var patches = MapVersions(cache?.Patches ?? [], markCurrent: false);
            return patches.Count == 0
                ? GamePatchCatalogLoadResult.Unavailable()
                : new GamePatchCatalogLoadResult(patches, IsFromCache: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedCacheFailure(exception))
        {
            return GamePatchCatalogLoadResult.Unavailable();
        }
    }

    private string GetCachePath() => Path.Combine(_cacheDirectory, "patches.json");

    private static string? NormalizePatch(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            && int.TryParse(parts[0], out var major)
            && int.TryParse(parts[1], out var minor)
                ? $"{major}.{minor}"
                : null;
    }

    private static bool IsExpectedCacheFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException;

    private sealed class DataDragonGamePatchCacheDto
    {
        public DateTimeOffset SavedAtUtc { get; init; }

        public List<string>? Patches { get; init; }
    }
}
