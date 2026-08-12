using System.Text.Json;
using System.Text.Encodings.Web;
using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Проверяет данные Coach Pack и строит JSON item set, понятный League.</summary>
public sealed class BuildLeagueItemSetExportUseCase
{
    private const int SummonersRiftMapId = 11;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private readonly IChampionCatalog _championCatalog;
    private readonly IItemCatalog _itemCatalog;

    public BuildLeagueItemSetExportUseCase(
        IChampionCatalog championCatalog,
        IItemCatalog itemCatalog)
    {
        _championCatalog = championCatalog
            ?? throw new ArgumentNullException(nameof(championCatalog));
        _itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
    }

    public async Task<LeagueItemSetExportBuildResult> ExecuteAsync(
        CoachPack pack,
        string title,
        IReadOnlyCollection<string> selectedBlockIds,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(selectedBlockIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        var selectedIds = selectedBlockIds.ToHashSet(StringComparer.Ordinal);
        var blocks = pack.ItemBlocks
            .Where(block => selectedIds.Contains(block.Id))
            .Where(static block => block.ItemIds.Count > 0)
            .ToArray();
        if (blocks.Length == 0)
        {
            return LeagueItemSetExportBuildResult.Failure(
                LeagueItemSetExportError.NoBlocksSelected);
        }

        var championTask = _championCatalog.LoadAsync(languageCode, cancellationToken);
        var itemTask = _itemCatalog.LoadAsync(pack.Patch, languageCode, cancellationToken);
        await Task.WhenAll(championTask, itemTask);

        var championResult = await championTask;
        if (!championResult.IsAvailable)
        {
            return LeagueItemSetExportBuildResult.Failure(
                LeagueItemSetExportError.ChampionCatalogUnavailable);
        }

        var champion = championResult.Champions.FirstOrDefault(entry =>
            string.Equals(entry.Id, pack.PlayerChampion, StringComparison.OrdinalIgnoreCase));
        if (champion is null || champion.NumericId <= 0)
        {
            return LeagueItemSetExportBuildResult.Failure(
                LeagueItemSetExportError.ChampionNotFound);
        }

        var itemResult = await itemTask;
        if (!itemResult.IsAvailable)
        {
            return LeagueItemSetExportBuildResult.Failure(
                LeagueItemSetExportError.ItemCatalogUnavailable);
        }

        var knownItemIds = itemResult.Items
            .Select(static item => item.Id)
            .ToHashSet();
        var missingItemIds = blocks
            .SelectMany(static block => block.ItemIds)
            .Distinct()
            .Where(itemId => !knownItemIds.Contains(itemId))
            .Order()
            .ToArray();
        if (missingItemIds.Length > 0)
        {
            return LeagueItemSetExportBuildResult.Failure(
                LeagueItemSetExportError.ItemsNotFound,
                missingItemIds);
        }

        var document = new LeagueItemSetDocument(
            title.Trim(),
            [champion.NumericId],
            [SummonersRiftMapId],
            blocks.Select(static block => new LeagueItemSetBlock(
                block.Name,
                block.ItemIds.Select(static itemId => new LeagueItemSetItem(
                    itemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    1)).ToArray())).ToArray());
        var json = JsonSerializer.Serialize(document, JsonOptions);
        return LeagueItemSetExportBuildResult.Success(
            json,
            blocks.Length,
            blocks.Sum(static block => block.ItemIds.Count));
    }

    private sealed record LeagueItemSetDocument(
        string Title,
        IReadOnlyList<int> AssociatedChampions,
        IReadOnlyList<int> AssociatedMaps,
        IReadOnlyList<LeagueItemSetBlock> Blocks);

    private sealed record LeagueItemSetBlock(
        string Type,
        IReadOnlyList<LeagueItemSetItem> Items);

    private sealed record LeagueItemSetItem(string Id, int Count);
}
