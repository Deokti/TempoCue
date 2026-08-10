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
            content.ItemPicker.Preview(boots);

            Assert.Equal("1200 золота", content.ItemPicker.PreviewedItemPriceText);
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
}
