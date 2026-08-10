using TempoCue.App.Localization;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет жизненный цикл произвольных блоков предметов и их порядок.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupItemBlocksViewModelTests
{
    [Fact]
    public void ItemBlocks_ManageIsolatedDraftAndAuthorOrder()
    {
        RunInEnglish(localization =>
        {
            var content = new MatchupContentViewModel(localization, TestItemCatalog.Ready());
            content.ShowItems();

            Assert.True(content.IsItemsSection);
            Assert.Equal("Items 0", content.ItemsTabLabel);

            var blocks = content.ItemBlocks;
            blocks.BeginAdd();
            Assert.False(blocks.Editor.CanSave);

            blocks.Editor.Name = "Starting items";
            blocks.Editor.WhenToChoose = "Before leaving base for the first time";
            Assert.True(blocks.Editor.CanSave);
            Assert.True(blocks.SaveEditor());

            var original = Assert.Single(blocks.Blocks);
            Assert.Equal("Starting items", original.Name);
            Assert.Equal("Before leaving base for the first time", original.WhenToChoose);
            Assert.Equal("0 items", original.ItemCountLabel);
            Assert.Equal("Items 1", content.ItemsTabLabel);

            blocks.BeginEdit(original);
            blocks.Editor.Name = "Unsaved name";
            blocks.Editor.Close();
            Assert.Equal("Starting items", original.Name);

            var copy = blocks.Duplicate(original);
            Assert.Equal("Starting items — copy", copy.Name);
            Assert.True(copy.CanMoveUp);
            Assert.True(blocks.MoveUp(copy));
            Assert.Equal(1, copy.Order);
            Assert.Equal(2, original.Order);

            Assert.True(blocks.Delete(copy));
            Assert.Single(blocks.Blocks);
            Assert.False(original.CanMoveUp);
            Assert.False(original.CanMoveDown);
        });
    }

    [Fact]
    public void ItemBlocks_EditorDuplicatesAndDeletesCurrentBlock()
    {
        RunInEnglish(localization =>
        {
            var blocks = new MatchupItemBlocksViewModel(localization);
            blocks.BeginAdd();
            blocks.Editor.Name = "Core build";

            var copy = Assert.IsType<MatchupItemBlockViewModel>(blocks.DuplicateEditor());
            Assert.Equal(2, blocks.Count);
            Assert.Equal(copy.Id, blocks.Editor.EditingBlockId);
            Assert.Equal("Core build — copy", copy.Name);

            Assert.True(blocks.DeleteEditor());
            Assert.False(blocks.Editor.IsOpen);
            Assert.Equal("Core build", Assert.Single(blocks.Blocks).Name);
        });
    }

    [Fact]
    public async Task ItemBlocks_CatalogSelectionPreservesAuthorOrder()
    {
        await RunInEnglishAsync(async localization =>
        {
            var content = new MatchupContentViewModel(localization, TestItemCatalog.Ready());
            content.SetItemCatalogContext("26.15", "en-US");
            await content.EnsureItemCatalogLoadedAsync();

            var blocks = content.ItemBlocks;
            blocks.BeginAdd();
            blocks.Editor.Name = "Core build";
            Assert.True(blocks.SaveEditor());
            var block = Assert.Single(blocks.Blocks);

            content.OpenItemBlockPicker(block);
            Assert.Equal(ItemPickerTarget.ItemBlockItems, content.ItemPicker.Target);
            var cleaver = content.ItemPicker.Items.Single(item => item.Id == 3071);
            var boots = content.ItemPicker.Items.Single(item => item.Id == 3047);
            content.ItemPicker.ToggleSelection(cleaver);
            content.ItemPicker.ToggleSelection(boots);
            Assert.True(content.ItemPicker.ConfirmSelection());
            Assert.Equal([3071, 3047], block.Items.Select(item => item.Id));

            Assert.True(blocks.MoveItemRight(block, block.Items[0]));
            Assert.Equal([3047, 3071], block.Items.Select(item => item.Id));

            content.OpenItemBlockPicker(block);
            Assert.Equal([3071, 3047], content.ItemPicker.SelectedItems.Select(item => item.Id));
            content.ItemPicker.ToggleSelection(boots);
            Assert.True(content.ItemPicker.ConfirmSelection());
            Assert.Equal(3071, Assert.Single(block.Items).Id);

            blocks.BeginEdit(block);
            content.OpenEditedItemBlockPicker();
            content.ItemPicker.ToggleSelection(boots);
            Assert.True(content.ItemPicker.ConfirmSelection());
            Assert.Equal([3071, 3047], blocks.Editor.Items.Select(item => item.Id));
            Assert.True(blocks.Editor.MoveItemLeft(blocks.Editor.Items[1]));
            Assert.True(blocks.SaveEditor());
            Assert.Equal([3047, 3071], block.Items.Select(item => item.Id));
        });
    }

    private static void RunInEnglish(Action<LocalizationService> test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("en-US");

        try
        {
            test(localization);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    private static async Task RunInEnglishAsync(Func<LocalizationService, Task> test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("en-US");

        try
        {
            await test(localization);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }
}
