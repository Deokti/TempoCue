namespace TempoCue.Application;

public enum ItemCatalogSource
{
    Network,
    Cache,
    Unavailable
}

/// <summary>Результат получения предметов с явным источником данных.</summary>
public sealed record ItemCatalogLoadResult(
    IReadOnlyList<ItemCatalogEntry> Items,
    ItemCatalogSource Source,
    bool CacheUpdateFailed = false)
{
    public bool IsAvailable =>
        Source != ItemCatalogSource.Unavailable && Items.Count > 0;

    public static ItemCatalogLoadResult Unavailable() =>
        new([], ItemCatalogSource.Unavailable);
}
