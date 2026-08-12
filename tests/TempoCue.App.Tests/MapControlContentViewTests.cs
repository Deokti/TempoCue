using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Core;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MapControlContentViewTests
{
    [Fact]
    public void ThirdStep_AddsObjectiveRuleThroughPickerAndInlineEditor() =>
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            var contentPage = OpenMapControlContent(context);

            Assert.Equal(Visibility.Visible, contentPage.Visibility);
            Assert.False(context.Coaching.MapControlEditor.CanMoveNext);
            WpfTestHost.Click(WpfTestHost.Find<Button>(contentPage, "AddMapObjectiveRuleButton"));

            var overlay = WpfTestHost.Find<Grid>(contentPage, "MapObjectivePickerOverlay");
            Assert.Equal(Visibility.Visible, overlay.Visibility);
            SaveVisualSnapshotIfRequested(
                context.Window,
                "map-control-editor-object-picker.png");
            Assert.Contains(
                context.Coaching.MapControlEditor.Content.ObjectiveOptions,
                option => option.Objective == MapObjectiveKind.ElementalDragon);
            context.Coaching.MapControlEditor.Content.BeginAddRule(
                MapObjectiveKind.ElementalDragon);
            WpfTestHost.Pump();

            var editor = context.Coaching.MapControlEditor.Content.Editor;
            Assert.True(editor.IsOpen);
            editor.Message = "Освободите линию и поставьте обзор заранее.";
            editor.WhyNow = "До появления дракона осталось мало времени.";
            editor.NextGoal = "Подойти к реке вместе с командой.";
            WpfTestHost.Pump();
            SaveVisualSnapshotIfRequested(
                context.Window,
                "map-control-editor-object-rule.png");
            var recurrenceOptions = WpfTestHost.Find<ListBox>(
                contentPage,
                "MapRuleRecurrenceListBox");
            recurrenceOptions.BringIntoView();
            WpfTestHost.Pump();
            SaveVisualSnapshotIfRequested(
                context.Window,
                "map-control-editor-object-recurrence.png");
            WpfTestHost.Click(WpfTestHost.Find<Button>(contentPage, "SaveMapRuleButton"));

            Assert.Single(context.Coaching.MapControlEditor.Content.Rules);
            Assert.True(context.Coaching.MapControlEditor.CanMoveNext);
            SaveVisualSnapshotIfRequested(context.Window, "map-control-editor-step3-objects.png");
        });

    private static MapControlContentView OpenMapControlContent(MainWindowTestContext context)
    {
        var libraryPage = OpenLibrary(context);
        WpfTestHost.Click(WpfTestHost.Find<Button>(libraryPage, "CreatePackButton"));
        var packTypePage = WpfTestHost.Find<PackTypeSelectionView>(
            context.CoachingPage,
            "PackTypeSelectionPage");
        WpfTestHost.Click(WpfTestHost.Find<Button>(packTypePage, "GameControlPackTypeButton"));

        var editorPage = context.MapControlEditorPage;
        var main = context.Coaching.MapControlEditor.MainInformation;
        main.PackName = "Контроль объектов";
        main.ShortDescription =
            "Напоминания о подготовке и появлении нейтральных объектов.";
        WpfTestHost.Pump();
        var next = WpfTestHost.Find<Button>(editorPage, "MapControlEditorNextButton");
        WpfTestHost.Click(next, DispatcherPriority.ApplicationIdle);
        WpfTestHost.Click(next, DispatcherPriority.ApplicationIdle);

        return WpfTestHost.Find<MapControlContentView>(editorPage, "MapControlContentPage");
    }
}
