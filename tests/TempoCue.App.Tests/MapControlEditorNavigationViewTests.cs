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
    public void ReopenAfterLeavingThirdStep_ResetsVisibleAndSelectedStep() => WpfTestHost.Run(() =>
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
        WpfTestHost.Click(gameControlButton, DispatcherPriority.ApplicationIdle);

        FillMainInformation(context, "Первый набор");
        var next = WpfTestHost.Find<Button>(
            context.MapControlEditorPage,
            "MapControlEditorNextButton");
        var back = WpfTestHost.Find<Button>(
            context.MapControlEditorPage,
            "MapControlEditorBackButton");
        WpfTestHost.Click(next, DispatcherPriority.ApplicationIdle);
        WpfTestHost.Click(next, DispatcherPriority.ApplicationIdle);
        Assert.Equal(3, context.Coaching.MapControlEditor.CurrentStep);

        WpfTestHost.Click(back, DispatcherPriority.ApplicationIdle);
        WpfTestHost.Click(back, DispatcherPriority.ApplicationIdle);
        WpfTestHost.Click(back, DispatcherPriority.ApplicationIdle);
        Assert.Equal(Visibility.Visible, packTypePage.Visibility);

        WpfTestHost.Click(gameControlButton, DispatcherPriority.ApplicationIdle);
        var steps = WpfTestHost.Find<ListBox>(
            context.MapControlEditorPage,
            "MapControlEditorStepsListBox");
        Assert.Equal(1, context.Coaching.MapControlEditor.CurrentStep);
        Assert.Equal(0, steps.SelectedIndex);

        FillMainInformation(context, "Второй набор");
        Assert.Equal(1, context.Coaching.MapControlEditor.CurrentStep);
        Assert.Equal(0, steps.SelectedIndex);
    });

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

        WpfTestHost.Click(
            WpfTestHost.Find<Button>(context.MapControlEditorPage, "MapControlEditorNextButton"),
            DispatcherPriority.ApplicationIdle);

        Assert.Equal(Visibility.Visible, WpfTestHost.Find<MapControlScopeView>(
            context.MapControlEditorPage,
            "MapControlScopePage").Visibility);
        Assert.Equal(Visibility.Collapsed, WpfTestHost.Find<MapControlMainInformationView>(
            context.MapControlEditorPage,
            "MapControlMainInformationPage").Visibility);
        Assert.True(stepItems[2].IsEnabled);
        SaveVisualSnapshotIfRequested(
            context.Window,
            "map-control-editor-step2.png");
    });

    private static void FillMainInformation(
        MainWindowTestContext context,
        string packName)
    {
        var form = context.Coaching.MapControlEditor.MainInformation;
        form.PackName = packName;
        form.ShortDescription =
            "Напоминания о подготовке и появлении нейтральных объектов.";
        WpfTestHost.Pump();
    }
}
