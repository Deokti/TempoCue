using TempoCue.Application;

namespace TempoCue.App.Tests;

internal sealed class TestItemCatalog(ItemCatalogLoadResult result) : IItemCatalog
{
    public static TestItemCatalog Ready() => new(
        new ItemCatalogLoadResult(
            [
                new ItemCatalogEntry(
                    3071,
                    "Чёрная секира",
                    "Предмет для затяжных физических сражений.",
                    new Uri("https://example.test/3071.png"),
                    3000,
                    ["Damage"],
                    ItemCatalogCategory.Legendary,
                    [
                        new ItemStatEntry(ItemStatKind.Health, 400),
                        new ItemStatEntry(ItemStatKind.AttackDamage, 40)
                    ]),
                new ItemCatalogEntry(
                    3047,
                    "Бронированные сапоги",
                    "Снижают получаемый физический урон.",
                    new Uri("https://example.test/3047.png"),
                    1200,
                    ["Boots", "Armor"],
                    ItemCatalogCategory.Boots,
                    [
                        new ItemStatEntry(ItemStatKind.Armor, 25),
                        new ItemStatEntry(ItemStatKind.MoveSpeed, 45)
                    ])
            ],
            ItemCatalogSource.Network));

    public Task<ItemCatalogLoadResult> LoadAsync(
        string gamePatch,
        string languageCode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(result);
}
