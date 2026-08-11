using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;

namespace TempoCue.App.Tests;

/// <summary>Проверяет поиск, фильтрацию и представление каталога предметов.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class ItemPickerViewModelTests
{
    [Fact]
    public async Task ItemPicker_FiltersSortsAndShowsConfirmedCatalogDetails()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var content = new MatchupContentViewModel(localization, TestItemCatalog.Ready());
            content.SetItemCatalogContext("26.15", "ru-RU");
            await content.EnsureItemCatalogLoadedAsync();
            content.OpenRecommendedItemPicker();

            Assert.Equal(6, content.ItemPicker.CategoryOptions.Count);
            content.ItemPicker.SelectCategory(content.ItemPicker.CategoryOptions.Single(
                option => option.Category == ItemFilterCategory.Boots));
            Assert.Equal(3047, Assert.Single(content.ItemPicker.FilteredItems).Id);

            content.ItemPicker.SelectCategory(content.ItemPicker.CategoryOptions.Single(
                option => option.Category == ItemFilterCategory.All));
            content.ItemPicker.SelectedSortOption = content.ItemPicker.SortOptions.Single(
                option => option.Mode == ItemSortMode.PriceDescending);
            Assert.Equal([3071, 3047], content.ItemPicker.FilteredItems.Select(item => item.Id));

            content.ItemPicker.Search = "сапоги";
            var boots = Assert.Single(content.ItemPicker.FilteredItems);
            Assert.Same(content.ItemPicker.FilteredItems, content.ItemPicker.FilteredItems);
            content.ItemPicker.Preview(boots);

            Assert.Equal("1200 золота", content.ItemPicker.PreviewedItemPriceText);
            Assert.Same(content.ItemPicker.PreviewedItemStats, content.ItemPicker.PreviewedItemStats);
            Assert.Contains(
                content.ItemPicker.PreviewedItemStats,
                stat => stat.Name == "Броня" && stat.Value == "+25");
            Assert.Contains("ID 3047", content.ItemPicker.PreviewedItemTechnicalText);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    [Fact]
    public async Task ItemPicker_OlderCatalogRequestCannotReplaceNewContext()
    {
        var catalog = new SequencedItemCatalog();
        var viewModel = new ItemPickerViewModel(catalog, LocalizationService.Instance);
        viewModel.SetContext("26.14", "ru-RU");
        var olderRequest = viewModel.EnsureCatalogLoadedAsync();

        viewModel.SetContext("26.15", "ru-RU");
        var currentRequest = viewModel.EnsureCatalogLoadedAsync();
        catalog.Complete("26.15", CreateResult(3071, "Текущий предмет"));
        await currentRequest;

        catalog.Complete("26.14", CreateResult(3047, "Устаревший предмет"));
        await olderRequest;

        Assert.Equal(CatalogViewState.Ready, viewModel.CatalogState);
        Assert.Equal(3071, Assert.Single(viewModel.Items).Id);
    }

    private static ItemCatalogLoadResult CreateResult(int id, string name) => new(
        [
            new ItemCatalogEntry(
                id,
                name,
                string.Empty,
                new Uri($"https://example.test/{id}.png"),
                1000,
                [],
                ItemCatalogCategory.Basic,
                [])
        ],
        ItemCatalogSource.Network);

    private sealed class SequencedItemCatalog : IItemCatalog
    {
        private readonly Dictionary<string, TaskCompletionSource<ItemCatalogLoadResult>> _requests = [];

        public Task<ItemCatalogLoadResult> LoadAsync(
            string gamePatch,
            string languageCode,
            CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<ItemCatalogLoadResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(gamePatch, completion);
            return completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete(string gamePatch, ItemCatalogLoadResult result) =>
            _requests[gamePatch].TrySetResult(result);
    }
}
