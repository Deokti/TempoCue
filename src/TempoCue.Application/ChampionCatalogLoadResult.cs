namespace TempoCue.Application;

public enum ChampionCatalogSource
{
    Network,
    Cache,
    Unavailable
}

/// <summary>Результат получения каталога с явным различием сети, кеша и ошибки.</summary>
public sealed record ChampionCatalogLoadResult(
    IReadOnlyList<ChampionCatalogEntry> Champions,
    ChampionCatalogSource Source,
    bool CacheUpdateFailed = false)
{
    public bool IsAvailable =>
        Source != ChampionCatalogSource.Unavailable && Champions.Count > 0;

    public static ChampionCatalogLoadResult Unavailable() =>
        new([], ChampionCatalogSource.Unavailable);
}
