using System.Text.Json;
using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class BuildLeagueItemSetExportUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_PreservesSelectedBlockOrderAndLeagueIdentifiers()
    {
        var useCase = CreateUseCase([3071, 3047]);
        var pack = CreatePack();

        var result = await useCase.ExecuteAsync(
            pack,
            "TempoCue — Urgot против Sett",
            ["boots", "core"],
            "ru-RU");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.BlockCount);
        Assert.Equal(2, result.ItemCount);
        using var document = JsonDocument.Parse(result.Json);
        var root = document.RootElement;
        Assert.Equal(6, root.GetProperty("associatedChampions")[0].GetInt32());
        Assert.Equal(11, root.GetProperty("associatedMaps")[0].GetInt32());
        var blocks = root.GetProperty("blocks");
        Assert.Equal("Ключевая сборка", blocks[0].GetProperty("type").GetString());
        Assert.Equal("3071", blocks[0].GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("Ботинки", blocks[1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_BlocksExportWhenPatchCatalogMissesAnItem()
    {
        var useCase = CreateUseCase([3071]);

        var result = await useCase.ExecuteAsync(
            CreatePack(),
            "TempoCue — Urgot против Sett",
            ["core", "boots"],
            "ru-RU");

        Assert.False(result.IsSuccess);
        Assert.Equal(LeagueItemSetExportError.ItemsNotFound, result.Error);
        Assert.Equal([3047], result.MissingItemIds);
        Assert.Empty(result.Json);
    }

    private static BuildLeagueItemSetExportUseCase CreateUseCase(
        IReadOnlyList<int> availableItemIds) => new(
        new StubChampionCatalog(),
        new StubItemCatalog(availableItemIds));

    private static CoachPack CreatePack() => new()
    {
        SchemaVersion = 2,
        Id = "test.export",
        Name = "Urgot против Sett",
        Patch = "26.15",
        PlayerChampion = "Urgot",
        OpponentChampion = "Sett",
        ItemBlocks =
        [
            new CoachItemBlockDefinition
            {
                Id = "core",
                Name = "Ключевая сборка",
                ItemIds = [3071]
            },
            new CoachItemBlockDefinition
            {
                Id = "boots",
                Name = "Ботинки",
                ItemIds = [3047]
            }
        ]
    };

    private sealed class StubChampionCatalog : IChampionCatalog
    {
        public Task<ChampionCatalogLoadResult> LoadAsync(
            string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChampionCatalogLoadResult(
                [new ChampionCatalogEntry("Urgot", 6, "Ургот", new Uri("https://example.test/Urgot.png"))],
                ChampionCatalogSource.Network));
    }

    private sealed class StubItemCatalog(IReadOnlyList<int> availableItemIds) : IItemCatalog
    {
        public Task<ItemCatalogLoadResult> LoadAsync(
            string gamePatch,
            string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ItemCatalogLoadResult(
                availableItemIds.Select(itemId => new ItemCatalogEntry(
                    itemId,
                    $"Item {itemId}",
                    string.Empty,
                    new Uri($"https://example.test/{itemId}.png"),
                    1000,
                    [],
                    ItemCatalogCategory.Legendary,
                    [])).ToArray(),
                ItemCatalogSource.Network));
    }
}
