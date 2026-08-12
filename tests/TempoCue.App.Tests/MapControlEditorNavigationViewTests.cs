using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.Views;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

/// <summary>Проверяет переход в редактор контроля карты и доступность его шагов.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MapControlEditorNavigationViewTests
{
    [Fact]
    public void PackTypeSelection_OpensMapControlEditor() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var libraryPage = OpenLibrary(context);
        WpfTestHost.Click(WpfTestHost.Find<Button>(libraryPage, "CreatePackButton"));
        var packTypePage = WpfTestHost.Find<PackTypeSelectionView>(
            context.CoachingPage,
            "PackTypeSelectionPage");

        var gameControlButton = WpfTestHost.Find<Button>(
            packTypePage,
            "GameControlPackTypeButton");
        Assert.True(gameControlButton.IsEnabled);
        WpfTestHost.Click(gameControlButton, DispatcherPriority.ApplicationIdle);

        Assert.Equal(Visibility.Collapsed, packTypePage.Visibility);
        Assert.Equal(Visibility.Visible, context.MapControlEditorPage.Visibility);
        Assert.Equal(
            24d,
            WpfTestHost.Find<TextBlock>(
                context.MapControlEditorPage,
                "MapControlEditorTitleTextBlock").FontSize);
        var steps = WpfTestHost.Find<ListBox>(
            context.MapControlEditorPage,
            "MapControlEditorStepsListBox");
        Assert.Equal(5, steps.Items.Count);
        Assert.Equal(0, steps.SelectedIndex);
        var stepItems = steps.Items.Cast<ListBoxItem>().ToArray();
        Assert.True(stepItems[0].IsEnabled);
        Assert.All(stepItems[1..], item => Assert.False(item.IsEnabled));
        SaveVisualSnapshotIfRequested(
            context.Window,
            "map-control-editor-step1.png");

        var form = context.Coaching.MapControlEditor.MainInformation;
        form.PackName = "Контроль объектов";
        form.ShortDescription =
            "Напоминания о подготовке и появлении нейтральных объектов.";
        WpfTestHost.Pump();

        Assert.True(stepItems[1].IsEnabled);
        Assert.True(WpfTestHost.Find<Button>(
            context.MapControlEditorPage,
            "MapControlEditorNextButton").IsEnabled);
    });
}
