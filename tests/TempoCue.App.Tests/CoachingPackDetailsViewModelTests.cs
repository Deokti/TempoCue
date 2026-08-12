using System.IO;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CoachingPackDetailsViewModelTests
{
    [Fact]
    public async Task OpenAsync_IgnoresCatalogResultFromPreviousPack()
    {
        var itemCatalog = new ControlledItemCatalog(honorCancellation: false);
        using var viewModel = CreateViewModel(itemCatalog);
        var firstPack = CreatePack("first", "26.14");
        var secondPack = CreatePack("second", "26.15");

        var firstOpen = viewModel.OpenAsync(CreateCard(firstPack));
        var secondOpen = viewModel.OpenAsync(CreateCard(secondPack));

        Assert.Equal(2, itemCatalog.RequestCount);
        Assert.True(itemCatalog.GetCancellationToken(0).IsCancellationRequested);

        itemCatalog.Complete(1, CreateCatalogResult("Актуальный предмет"));
        await secondOpen;

        Assert.Same(secondPack, viewModel.Pack);
        Assert.False(viewModel.IsItemCatalogLoading);
        Assert.Equal(
            "Актуальный предмет",
            Assert.Single(Assert.Single(viewModel.ItemBlocks).Items).DisplayName);

        itemCatalog.Complete(0, CreateCatalogResult("Устаревший предмет"));
        await firstOpen;

        Assert.Same(secondPack, viewModel.Pack);
        Assert.False(viewModel.IsItemCatalogLoading);
        Assert.Empty(viewModel.ItemCatalogMessage);
        Assert.Equal(
            "Актуальный предмет",
            Assert.Single(Assert.Single(viewModel.ItemBlocks).Items).DisplayName);
    }

    [Fact]
    public async Task Dispose_ClosesExportAndCancelsItsCatalogLoad()
    {
        var itemCatalog = new ControlledItemCatalog(honorCancellation: false);
        var viewModel = CreateViewModel(itemCatalog);
        var pack = CreatePack("pack", "26.15");
        var openDetails = viewModel.OpenAsync(CreateCard(pack));
        itemCatalog.Complete(0, CreateCatalogResult("Чёрная секира"));
        await openDetails;
        var openExport = viewModel.OpenExportAsync();
        Assert.True(viewModel.Export.IsOpen);
        Assert.True(viewModel.Export.IsBuilding);

        viewModel.Dispose();

        Assert.True(itemCatalog.GetCancellationToken(1).IsCancellationRequested);
        itemCatalog.Complete(1, CreateCatalogResult("Чёрная секира"));
        await openExport;
        Assert.False(viewModel.Export.IsOpen);
        Assert.False(viewModel.Export.IsBuilding);
    }

    [Fact]
    public async Task ExportCoachPackAsync_WritesCurrentPackAndReportsSuccess()
    {
        var itemCatalog = new ControlledItemCatalog(honorCancellation: false);
        using var viewModel = CreateViewModel(itemCatalog);
        var pack = CreatePack("pack-for-export", "26.15");
        var openDetails = viewModel.OpenAsync(CreateCard(pack));
        itemCatalog.Complete(0, CreateCatalogResult("Чёрная секира"));
        await openDetails;
        await using var destination = new MemoryStream();

        var exported = await viewModel.ExportCoachPackAsync(destination);

        Assert.True(exported);
        Assert.True(viewModel.CanExportCoachPack);
        Assert.True(viewModel.HasCoachPackExportMessage);
        Assert.False(viewModel.HasCoachPackExportError);
        destination.Position = 0;
        var exportedPack = await CoachPackLoader.LoadAsync(destination);
        Assert.Equal(pack.Id, exportedPack.Id);
        Assert.Equal(pack.Name, exportedPack.Name);
        var exportedBlock = Assert.Single(exportedPack.ItemBlocks);
        Assert.Equal(pack.ItemBlocks[0].Id, exportedBlock.Id);
        Assert.Equal(pack.ItemBlocks[0].Name, exportedBlock.Name);
        Assert.Equal(pack.ItemBlocks[0].ItemIds, exportedBlock.ItemIds);
    }

    private static CoachPack CreatePack(string id, string patch) =>
        TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Id = id,
            Name = id,
            Patch = patch,
            PersistentTips = [],
            ItemBlocks =
            [
                new CoachItemBlockDefinition
                {
                    Id = "core",
                    Name = "Основная сборка",
                    ItemIds = [3071]
                }
            ]
        };

    private static CoachingPackDetailsViewModel CreateViewModel(IItemCatalog itemCatalog) => new(
        LocalizationService.Instance,
        itemCatalog,
        new BuildLeagueItemSetExportUseCase(
            TestChampionCatalog.Ready(),
            itemCatalog),
        new ExportCoachPackUseCase());

    private static CoachingPackCardViewModel CreateCard(CoachPack pack) => new(
        pack,
        CoachingPackOrigin.CreatedByUser,
        LocalCoachPackState.Enabled,
        pack.Name,
        "Urgot",
        "Sett",
        "URG",
        "SET",
        null,
        null,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);

    private static ItemCatalogLoadResult CreateCatalogResult(string displayName) => new(
        [
            new ItemCatalogEntry(
                3071,
                displayName,
                string.Empty,
                new Uri("https://example.test/3071.png"),
                3000,
                [],
                ItemCatalogCategory.Legendary,
                [])
        ],
        ItemCatalogSource.Network);

}
