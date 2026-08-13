using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Application;
using TempoCue.Coaching;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MapControlSaveViewTests
{
    [Fact]
    public void SaveStep_CreatesDraftAndReturnsToLibrary() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        OpenMapControlEditor(context);
        var editor = context.Coaching.MapControlEditor;
        editor.MainInformation.PackName = "Контроль ранних объектов";
        editor.MainInformation.ShortDescription =
            "Напоминания о подготовке к первым нейтральным объектам карты.";
        Assert.True(editor.MoveNext());
        Assert.True(editor.MoveNext());
        AddTimedReminder(editor.Content);
        Assert.True(editor.MoveNext());
        Assert.True(editor.MoveNext());
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var page = WpfTestHost.Find<MapControlSaveView>(
            context.MapControlEditorPage,
            "MapControlSavePage");
        Assert.Equal(5, editor.CurrentStep);
        Assert.Equal(Visibility.Visible, page.Visibility);
        Assert.True(editor.Save.CanSave);
        Assert.Equal(1, editor.Save.TimedReminderCount);
        Assert.Equal(Visibility.Visible, WpfTestHost.Find<Button>(
            context.MapControlEditorPage,
            "MapControlSaveDraftButton").Visibility);
        SaveVisualSnapshotIfRequested(
            context.Window,
            "map-control-editor-step5-save.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(
            context.MapControlEditorPage,
            "MapControlSaveDraftButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var savedPack = Assert.IsType<MapControlPack>(context.MapControlPackStore.SavedPack);
        Assert.Equal(LocalCoachPackState.Draft, context.MapControlPackStore.SavedState);
        Assert.Equal("Контроль ранних объектов", savedPack.Name);
        Assert.Single(savedPack.TimedReminders);
        Assert.Equal(CoachingScreen.Library, context.Coaching.CurrentScreen);
    });

    private static void OpenMapControlEditor(MainWindowTestContext context)
    {
        var library = OpenLibrary(context);
        WpfTestHost.Click(WpfTestHost.Find<Button>(library, "CreatePackButton"));
        var packType = WpfTestHost.Find<PackTypeSelectionView>(
            context.CoachingPage,
            "PackTypeSelectionPage");
        WpfTestHost.Click(
            WpfTestHost.Find<Button>(packType, "GameControlPackTypeButton"),
            DispatcherPriority.ApplicationIdle);
    }

    private static void AddTimedReminder(MapControlContentViewModel content)
    {
        content.TimedReminders.BeginAdd();
        var form = content.TimedReminders.Editor;
        form.GameTimeInput = "03:15";
        form.Title = "Ранний обзор";
        form.Message = "Проверьте обзор вокруг первой цели.";
        form.WhyNow = "Скоро начнётся важное игровое событие.";
        Assert.True(content.TimedReminders.SaveEditor());
    }
}
