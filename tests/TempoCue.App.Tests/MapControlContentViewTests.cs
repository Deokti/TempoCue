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
            var tabsPanel = WpfTestHost.Find<Border>(contentPage, "MapContentTabsPanel");
            Assert.Equal(new Thickness(4), tabsPanel.Padding);
            Assert.Equal(new CornerRadius(12), tabsPanel.CornerRadius);
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

    [Fact]
    public void ThirdStep_AddsPersistentTipFromDedicatedTab() =>
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            var contentPage = OpenMapControlContent(context);
            var content = context.Coaching.MapControlEditor.Content;

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                contentPage,
                "MapPersistentTipsTabButton"));
            Assert.True(content.IsPersistentTipsSection);
            var persistentPage = WpfTestHost.Find<MapControlPersistentTipsView>(
                contentPage,
                "MapControlPersistentTipsPage");
            Assert.Equal(Visibility.Visible, persistentPage.Visibility);

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                persistentPage,
                "AddMapPersistentTipButton"));
            content.PersistentTips.Editor.Title = "Не отдавайте реку без обзора";
            content.PersistentTips.Editor.Message =
                "Проверяйте подходы к объекту до начала командного боя.";
            content.PersistentTips.Editor.SelectedScope = new(
                TempoCue.Coaching.MapControlPersistentTipScopeKind.SpecificObjective,
                MapObjectiveKind.ElementalDragon);
            WpfTestHost.Pump();
            SaveVisualSnapshotIfRequested(
                context.Window,
                "map-control-editor-persistent-tip-editor.png");

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                persistentPage,
                "SaveMapPersistentTipButton"));

            Assert.Single(content.PersistentTips.Tips);
            Assert.Contains("1", content.PersistentTipsTabLabel, StringComparison.Ordinal);
            SaveVisualSnapshotIfRequested(
                context.Window,
                "map-control-editor-persistent-tips.png");
        });

    [Fact]
    public void ThirdStep_AddsTimedReminderAndKeepsSortedListVisible() =>
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            var contentPage = OpenMapControlContent(context);
            var content = context.Coaching.MapControlEditor.Content;

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                contentPage,
                "MapTimedRemindersTabButton"));
            Assert.True(content.IsTimedRemindersSection);
            var timedPage = WpfTestHost.Find<MapControlTimedRemindersView>(
                contentPage,
                "MapControlTimedRemindersPage");
            Assert.Equal(Visibility.Visible, timedPage.Visibility);

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                timedPage,
                "AddMapTimedReminderButton"));
            var contextFields = WpfTestHost.Find<Grid>(
                timedPage,
                "MapTimedReminderContextGrid");
            Assert.Equal(3, contextFields.ColumnDefinitions.Count);
            Assert.Equal(new GridLength(16), contextFields.ColumnDefinitions[1].Width);
            content.TimedReminders.Editor.GameTimeInput = "30";
            content.TimedReminders.Editor.Title = "Ранний ганк";
            content.TimedReminders.Editor.Message =
                "Не уходи далеко от своей башни без информации о леснике.";
            content.TimedReminders.Editor.WhyNow =
                "Лесник обычно заканчивает первый круг и ищет уязвимую линию.";
            content.TimedReminders.Editor.NextGoal =
                "Отойди ближе к башне и дождись безопасного обзора.";
            WpfTestHost.Pump();
            SaveVisualSnapshotIfRequested(
                context.Window,
                "map-control-editor-timed-reminder-editor.png");

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                timedPage,
                "SaveMapTimedReminderButton"));

            Assert.Single(content.TimedReminders.Reminders);
            Assert.Equal("30:00", content.TimedReminders.Reminders[0].GameTimeLabel);
            SaveVisualSnapshotIfRequested(
                context.Window,
                "map-control-editor-timed-reminders.png");
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
