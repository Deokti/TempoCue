using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет пользовательский сценарий вкладки блоков предметов.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupItemBlocksViewTests
{
    [Fact]
    public void ItemBlocksView_AddsBlockAndKeepsItWhenSwitchingSections()
    {
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            context.Show();
            context.OpenMatchupEditor();
            context.CompleteMainInformationAndOpenChampions();
            context.SelectChampionsAndOpenContent();

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ContentPage,
                "MatchupItemsTabButton"));
            Assert.Equal(Visibility.Visible, context.ItemBlocksView.Visibility);

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ItemBlocksView,
                "AddItemBlockButton"));
            var blocks = context.Coaching.MatchupEditor.Content.ItemBlocks;
            blocks.Editor.Name = "Стартовые предметы";
            blocks.Editor.WhenToChoose = "Перед первым выходом на линию";
            WpfTestHost.Pump();

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ItemBlocksView,
                "SaveItemBlockButton"));
            Assert.Single(blocks.Blocks);
            Assert.Equal(
                "Предметы 1",
                context.Coaching.MatchupEditor.Content.ItemsTabLabel);

            var content = context.Coaching.MatchupEditor.Content;
            content.EnsureItemCatalogLoadedAsync().GetAwaiter().GetResult();
            WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
            context.ItemBlocksView.UpdateLayout();
            var addItemButton = Assert.Single(
                WpfTestHost.FindVisualDescendants<Button>(context.ItemBlocksView),
                button => button.IsVisible && Equals(button.Content, "+ Предмет"));
            WpfTestHost.Click(addItemButton);
            Assert.Equal(Visibility.Visible, context.ItemPickerDialog.Visibility);
            Assert.Equal(ItemPickerTarget.ItemBlockItems, content.ItemPicker.Target);

            content.ItemPicker.ToggleSelection(content.ItemPicker.Items.Single(
                item => item.Id == 3071));
            content.ItemPicker.ToggleSelection(content.ItemPicker.Items.Single(
                item => item.Id == 3047));
            WpfTestHost.Pump();
            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ItemPickerDialog,
                "ConfirmItemSelectionButton"));
            Assert.Equal(2, Assert.Single(blocks.Blocks).ItemCount);
            Assert.Equal(Visibility.Collapsed, context.ItemPickerDialog.Visibility);

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ContentPage,
                "MatchupPhasesTabButton"));
            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ContentPage,
                "MatchupItemsTabButton"));

            Assert.Single(blocks.Blocks);
            Assert.Equal(Visibility.Visible, context.ItemBlocksView.Visibility);
        });
    }
}
