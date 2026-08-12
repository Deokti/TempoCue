using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Coaching;

namespace TempoCue.App.Tests;

/// <summary>Проверяет последовательность первой итерации редактора контроля карты.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MapControlEditorViewModelTests
{
    [Fact]
    public async Task NewEditor_UnlocksScopeOnlyAfterValidMainInformation()
    {
        var editor = CreateEditor();

        await editor.BeginNewAsync();

        Assert.Equal(1, editor.CurrentStep);
        Assert.False(editor.CanMoveNext);
        Assert.True(editor.CanNavigateToStep1);
        Assert.False(editor.CanNavigateToStep2);
        Assert.False(editor.NavigateToStep(3));

        editor.MainInformation.PackName = "Контроль объектов";
        editor.MainInformation.ShortDescription =
            "Напоминания о подготовке и появлении нейтральных объектов.";

        Assert.True(editor.MainInformation.IsComplete);
        Assert.True(editor.CanMoveNext);
        Assert.True(editor.MoveNext());
        Assert.Equal(2, editor.CurrentStep);
        Assert.True(editor.CanMoveNext);
        Assert.True(editor.CanNavigateToStep3);
    }

    [Fact]
    public void Scope_TracksMultipleRolesAndNeverAllowsEmptySelection()
    {
        var editor = CreateEditor();
        var scope = editor.Scope;

        Assert.True(scope.AllRolesSelected);
        Assert.Equal(5, scope.SelectedRoles.Count);

        scope.RoleOptions.Single(option => option.Role == TempoCue.Coaching.MapControlRole.Top)
            .IsSelected = true;
        scope.RoleOptions.Single(option => option.Role == TempoCue.Coaching.MapControlRole.Jungle)
            .IsSelected = true;

        Assert.False(scope.AllRolesSelected);
        Assert.Equal(2, scope.SelectedRoles.Count);

        foreach (var option in scope.RoleOptions.ToArray())
        {
            option.IsSelected = false;
        }

        Assert.True(scope.IsComplete);
        Assert.Single(scope.SelectedRoles);
    }

    [Fact]
    public async Task VisitedScope_AllowsReturnToCompletedMainInformation()
    {
        var editor = CreateEditor();
        await editor.BeginNewAsync();
        editor.MainInformation.PackName = "Контроль объектов";
        editor.MainInformation.ShortDescription =
            "Напоминания о подготовке и появлении нейтральных объектов.";
        Assert.True(editor.MoveNext());

        Assert.True(editor.NavigateToStep(1));
        Assert.Equal(1, editor.CurrentStep);
        Assert.True(editor.CanNavigateToStep2);
        Assert.True(editor.NavigateToStep(2));
    }

    [Fact]
    public async Task CompletedContent_OpensVerificationWithTimelineAndSummary()
    {
        var editor = CreateEditor();
        await editor.BeginNewAsync();
        CompleteMainInformation(editor);
        Assert.True(editor.MoveNext());
        Assert.True(editor.MoveNext());
        AddTimedReminder(editor.Content, "Ранний обзор", "03:15");

        Assert.True(editor.MoveNext());

        Assert.Equal(4, editor.CurrentStep);
        Assert.True(editor.Verification.CanContinue);
        Assert.Equal(0, editor.Verification.ErrorCount);
        Assert.Equal(0, editor.Verification.WarningCount);
        Assert.Equal(1, editor.Verification.TimedReminderCount);
        Assert.Single(editor.Verification.TimelineMarkers);
        Assert.Contains(
            editor.Verification.TimelineMarkers,
            marker => marker.IsUserReminder && marker.Title == "Ранний обзор");
    }

    [Fact]
    public async Task ObjectiveRule_AddsOnlyItsSystemEventToTimeline()
    {
        var editor = CreateEditor();
        await editor.BeginNewAsync();
        CompleteMainInformation(editor);
        editor.Content.SetPatch(editor.MainInformation.GamePatch);
        editor.Content.BeginAddRule(TempoCue.Core.MapObjectiveKind.ElementalDragon);
        editor.Content.Editor.Message = "Подготовьте обзор у реки.";
        editor.Content.Editor.WhyNow = "Дракон скоро появится.";
        Assert.True(editor.Content.SaveEditor());

        editor.Verification.Update(editor.MainInformation, editor.Scope, editor.Content);

        var systemMarker = Assert.Single(
            editor.Verification.TimelineMarkers,
            marker => !marker.IsUserReminder);
        Assert.Equal("05:00", systemMarker.TimeLabel);
        Assert.DoesNotContain(
            editor.Verification.TimelineMarkers,
            marker => marker.TimeLabel is "08:00" or "15:00" or "20:00");
    }

    [Fact]
    public async Task FuturePatch_ShowsWarningWithoutBlockingVerification()
    {
        var editor = CreateEditor();
        await editor.BeginNewAsync();
        editor.MainInformation.PatchSelection.SelectPatch("26.16");
        CompleteMainInformation(editor);
        editor.Content.SetPatch(editor.MainInformation.GamePatch);
        AddTimedReminder(editor.Content, "Подготовка", "10:00");

        editor.Verification.Update(editor.MainInformation, editor.Scope, editor.Content);

        Assert.True(editor.Verification.CanContinue);
        Assert.Equal(0, editor.Verification.ErrorCount);
        Assert.Equal(1, editor.Verification.WarningCount);
        Assert.Contains("16.15", editor.Verification.Issues.Single().Message);
    }

    [Fact]
    public async Task CompletedVerification_OpensSaveStepAndSavesEnabledPack()
    {
        var store = new TestMapControlPackStore();
        var editor = CreateEditor(store);
        await editor.BeginNewAsync();
        CompleteMainInformation(editor);
        Assert.True(editor.MoveNext());
        Assert.True(editor.MoveNext());
        AddTimedReminder(editor.Content, "Подготовка к объекту", "03:15");
        Assert.True(editor.MoveNext());

        Assert.True(editor.MoveNext());

        Assert.Equal(5, editor.CurrentStep);
        Assert.True(editor.Save.CanSave);
        Assert.Equal(1, editor.Save.TimedReminderCount);
        Assert.True(await editor.Save.SaveAsync(LocalCoachPackState.Enabled));
        Assert.Equal(LocalCoachPackState.Enabled, store.SavedState);
        Assert.NotNull(store.SavedPack);
        Assert.Equal(editor.MainInformation.PackName, store.SavedPack.Name);
        Assert.Single(store.SavedPack.TimedReminders);
    }

    [Fact]
    public async Task ExternalSource_AllowsMissingLinkButRequiresTitleAndAuthor()
    {
        var store = new TestMapControlPackStore();
        var editor = CreateEditor(store);
        await editor.BeginNewAsync();
        CompleteMainInformation(editor);
        Assert.True(editor.MoveNext());
        Assert.True(editor.MoveNext());
        AddTimedReminder(editor.Content, "Подготовка к объекту", "03:15");
        Assert.True(editor.MoveNext());
        Assert.True(editor.MoveNext());

        editor.Save.SelectedSourceKind = CoachPackSourceKind.Article;
        Assert.False(editor.Save.CanSave);
        editor.Save.SourceTitle = "Гайд по контролю карты";
        editor.Save.SourceAuthor = "Автор гайда";

        Assert.True(editor.Save.CanSave);
        Assert.True(await editor.Save.SaveAsync(LocalCoachPackState.Draft));
        var savedPack = Assert.IsType<MapControlPack>(store.SavedPack);
        var savedSource = Assert.IsType<CoachPackSource>(savedPack.Source);
        Assert.Equal(string.Empty, savedSource.Url);
    }

    private static void CompleteMainInformation(MapControlEditorViewModel editor)
    {
        editor.MainInformation.PackName = "Контроль объектов";
        editor.MainInformation.ShortDescription =
            "Напоминания о подготовке и появлении нейтральных объектов.";
    }

    private static void AddTimedReminder(
        MapControlContentViewModel content,
        string title,
        string gameTime)
    {
        content.TimedReminders.BeginAdd();
        var form = content.TimedReminders.Editor;
        form.GameTimeInput = gameTime;
        form.Title = title;
        form.Message = "Проверьте обзор вокруг цели.";
        form.WhyNow = "Скоро начнётся важное игровое событие.";
        Assert.True(content.TimedReminders.SaveEditor());
    }

    private static MapControlEditorViewModel CreateEditor(
        TestMapControlPackStore? store = null) => new(
        LocalizationService.Instance,
        TestGamePatchCatalog.Ready(),
        new SaveMapControlPackUseCase(store ?? new TestMapControlPackStore()));
}
