using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class LeagueItemSetExportDialogViewModelTests
{
    [Fact]
    public async Task RefreshAsync_AppliesOnlyLatestCapturedRequest()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("en-US");
        try
        {
            var itemCatalog = new ControlledItemCatalog();
            var viewModel = CreateViewModel(localization, itemCatalog);
            var pack = TestCoachPackFactory.CreateDetailedMatchup();
            var openTask = viewModel.OpenAsync(pack, "First title", CreateBlocks(pack));
            Assert.Equal(1, itemCatalog.RequestCount);

            viewModel.Title = "Second title";
            Assert.True(viewModel.Blocks[1].IsSelected);
            viewModel.Blocks[1].IsSelected = false;
            var refreshTask = viewModel.RefreshAsync();
            Assert.Equal(2, itemCatalog.RequestCount);

            viewModel.Title = "Changed after start";
            viewModel.Blocks[1].IsSelected = true;
            await openTask;

            Assert.True(viewModel.IsBuilding);
            Assert.False(viewModel.CanExport);

            itemCatalog.Complete(1, CreateAvailableItems());
            await refreshTask;

            Assert.False(viewModel.IsBuilding);
            Assert.True(viewModel.CanExport);
            Assert.Contains("\"title\": \"Second title\"", viewModel.Json);
            Assert.Contains("\"id\": \"3071\"", viewModel.Json);
            Assert.DoesNotContain("\"id\": \"3047\"", viewModel.Json);
            itemCatalog.Complete(0, CreateAvailableItems());
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Fact]
    public async Task OpenAndClose_CancelPreviousBuildsWithoutRestoringTheirState()
    {
        var localization = LocalizationService.Instance;
        var itemCatalog = new ControlledItemCatalog();
        var viewModel = CreateViewModel(localization, itemCatalog);
        var pack = TestCoachPackFactory.CreateDetailedMatchup();
        var blocks = CreateBlocks(pack);

        var firstOpenTask = viewModel.OpenAsync(pack, "First title", blocks);
        var secondOpenTask = viewModel.OpenAsync(pack, "Second title", blocks);
        Assert.Equal(2, itemCatalog.RequestCount);

        await firstOpenTask;
        Assert.True(viewModel.IsOpen);
        Assert.True(viewModel.IsBuilding);
        Assert.Equal("Second title", viewModel.Title);

        viewModel.Close();
        await secondOpenTask;
        itemCatalog.Complete(0, CreateAvailableItems());
        itemCatalog.Complete(1, CreateAvailableItems());
        await Task.Yield();

        Assert.False(viewModel.IsOpen);
        Assert.False(viewModel.IsBuilding);
        Assert.False(viewModel.CanExport);
        Assert.Empty(viewModel.Json);
        Assert.Empty(viewModel.StatusMessage);
        Assert.Empty(viewModel.Blocks);
    }

    private static LeagueItemSetExportDialogViewModel CreateViewModel(
        LocalizationService localization,
        IItemCatalog itemCatalog) => new(
        localization,
        new BuildLeagueItemSetExportUseCase(
            TestChampionCatalog.Ready(),
            itemCatalog));

    private static IReadOnlyList<CoachingPackItemBlockDetailsViewModel> CreateBlocks(
        CoachPack pack) => pack.ItemBlocks
        .Select(block => new CoachingPackItemBlockDetailsViewModel(
            block.Id,
            block.Name,
            block.WhenToChoose ?? string.Empty,
            $"{block.ItemIds.Count} items",
            block.ItemIds
                .Select(itemId => new CoachingPackItemDetailsViewModel(
                    itemId,
                    $"Item {itemId}",
                    null))
                .ToArray()))
        .ToArray();

    private static ItemCatalogLoadResult CreateAvailableItems() => new(
        [
            CreateItem(3071),
            CreateItem(3047)
        ],
        ItemCatalogSource.Network);

    private static ItemCatalogEntry CreateItem(int id) => new(
        id,
        $"Item {id}",
        string.Empty,
        new Uri($"https://example.test/{id}.png"),
        1000,
        [],
        ItemCatalogCategory.Legendary,
        []);

    private sealed class ControlledItemCatalog : IItemCatalog
    {
        private readonly List<TaskCompletionSource<ItemCatalogLoadResult>> _requests = [];

        public int RequestCount => _requests.Count;

        public Task<ItemCatalogLoadResult> LoadAsync(
            string gamePatch,
            string languageCode,
            CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<ItemCatalogLoadResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(completion);
            return completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete(int requestIndex, ItemCatalogLoadResult result) =>
            _requests[requestIndex].TrySetResult(result);
    }
}
