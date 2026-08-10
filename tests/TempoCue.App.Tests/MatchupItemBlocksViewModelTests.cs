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
}
