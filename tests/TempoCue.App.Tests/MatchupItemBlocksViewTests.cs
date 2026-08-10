using System.Windows;
using System.Windows.Controls;

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
