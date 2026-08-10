namespace TempoCue.Application;

/// <summary>Причина, по которой безопасный экспорт item set не может быть построен.</summary>
public enum LeagueItemSetExportError
{
    None,
    NoBlocksSelected,
    ChampionCatalogUnavailable,
    ChampionNotFound,
    ItemCatalogUnavailable,
    ItemsNotFound
}

/// <summary>Результат подготовки переносимого League item set JSON.</summary>
public sealed record LeagueItemSetExportBuildResult(
    LeagueItemSetExportError Error,
    string Json,
    int BlockCount,
    int ItemCount,
    IReadOnlyList<int> MissingItemIds)
{
    public bool IsSuccess => Error == LeagueItemSetExportError.None;

    public static LeagueItemSetExportBuildResult Success(
        string json,
        int blockCount,
        int itemCount) =>
        new(LeagueItemSetExportError.None, json, blockCount, itemCount, []);

    public static LeagueItemSetExportBuildResult Failure(
        LeagueItemSetExportError error,
        IReadOnlyList<int>? missingItemIds = null) =>
        new(error, string.Empty, 0, 0, missingItemIds ?? []);
}
