using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using TempoCue.App.Views;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

/// <summary>Проверяет переход в редактор контроля карты и доступность его шагов.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MapControlEditorNavigationViewTests
{
    [Fact]
    public void DirectStepNavigation_KeepsSelectedStepBoundToViewModel() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        OpenMapControlEditor(context);
        FillMainInformation(context, "Контроль объектов");
        var steps = WpfTestHost.Find<ListBox>(
            context.MapControlEditorPage,
            "MapControlEditorStepsListBox");
        var stepItems = steps.Items.Cast<ListBoxItem>().ToArray();

        WpfTestHost.Click(stepItems[1], DispatcherPriority.ApplicationIdle);

        Assert.Equal(2, context.Coaching.MapControlEditor.CurrentStep);
        Assert.True(BindingOperations.IsDataBound(
            steps,
            Selector.SelectedIndexProperty));

        WpfTestHost.Click(
            WpfTestHost.Find<Button>(context.MapControlEditorPage, "MapControlEditorBackButton"),
            DispatcherPriority.ApplicationIdle);
        Assert.Equal(0, steps.SelectedIndex);

        WpfTestHost.Click(
            WpfTestHost.Find<Button>(context.MapControlEditorPage, "MapControlEditorNextButton"),
            DispatcherPriority.ApplicationIdle);
        Assert.Equal(1, steps.SelectedIndex);
    });

    [Fact]
    public void ReopenAfterLeavingThirdStep_ResetsVisibleAndSelectedStep() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var (packTypePage, gameControlButton) = OpenMapControlEditor(context);

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
        var (packTypePage, gameControlButton) = OpenMapControlEditor(context);
        Assert.True(gameControlButton.IsEnabled);

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
        Assert.Equal(2, context.Coaching.MapControlEditor.CurrentStep);
        Assert.Equal(1, steps.SelectedIndex);
        Assert.True(stepItems[2].IsEnabled);
        SaveVisualSnapshotIfRequested(
            context.Window,
            "map-control-editor-step2.png");

        WpfTestHost.Click(
            WpfTestHost.Find<Button>(context.MapControlEditorPage, "MapControlEditorNextButton"),
            DispatcherPriority.ApplicationIdle);

        Assert.Equal(3, context.Coaching.MapControlEditor.CurrentStep);
        Assert.Equal(2, steps.SelectedIndex);
        Assert.True(stepItems[2].IsSelected);
        SaveVisualSnapshotIfRequested(
            context.Window,
            "map-control-editor-step3-navigation.png");
    });

    private static (PackTypeSelectionView Page, Button GameControlButton) OpenMapControlEditor(
        MainWindowTestContext context)
    {
        var libraryPage = OpenLibrary(context);
        WpfTestHost.Click(WpfTestHost.Find<Button>(libraryPage, "CreatePackButton"));
        var packTypePage = WpfTestHost.Find<PackTypeSelectionView>(
            context.CoachingPage,
            "PackTypeSelectionPage");
        var gameControlButton = WpfTestHost.Find<Button>(
            packTypePage,
            "GameControlPackTypeButton");
        WpfTestHost.Click(gameControlButton, DispatcherPriority.ApplicationIdle);
        return (packTypePage, gameControlButton);
    }

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
