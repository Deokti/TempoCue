using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Coaching;
using TempoCue.Core;

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

    [Fact]
    public async Task LoadExistingPack_PopulatesAllStepsAndPreservesIdentityOnSave()
    {
        var store = new TestMapControlPackStore();
        var editor = CreateEditor(store);
        var pack = TestMapControlPackFactory.Create("map-control.edit-test");

        await editor.LoadAsync(pack, LocalCoachPackState.Enabled, createCopy: false);

        Assert.True(editor.IsEditingExisting);
        Assert.True(editor.ReturnsToPackDetails);
        Assert.Equal(pack.Name, editor.MainInformation.PackName);
        Assert.Single(editor.Content.Rules);
        Assert.True(editor.CanNavigateToStep5);
        Assert.True(editor.NavigateToStep(5));
        Assert.True(await editor.Save.SaveAsync(LocalCoachPackState.Enabled));
        Assert.Equal(pack.Id, store.SavedPack?.Id);
    }

    [Fact]
    public async Task LoadPackWithVisionRules_KeepsThemThroughSave()
    {
        var store = new TestMapControlPackStore();
        var editor = CreateEditor(store);
        var pack = TestMapControlPackFactory.Create("map-control.vision-test") with
        {
            VisionRules =
            [
                new MapVisionRule(
                    "enemy-jungler-appeared",
                    PlayerPosition.Jungle,
                    MapVisionTransition.Appeared,
                    MapObjectiveReminderPriority.Important,
                    "Вражеский лесник виден на карте.",
                    "Положение лесника — самая скоропортящаяся информация.",
                    "Реши, даёт ли это окно.")
            ]
        };

        await editor.LoadAsync(pack, LocalCoachPackState.Enabled, createCopy: false);
        Assert.True(editor.NavigateToStep(5));
        Assert.True(await editor.Save.SaveAsync(LocalCoachPackState.Enabled));

        // Редактор правил наблюдения ещё не показывает, но обязан пронести их
        // через себя: иначе правка копии набора про лесника молча стирала бы
        // единственное, ради чего набор существует.
        var saved = Assert.Single(store.SavedPack!.VisionRules);
        Assert.Equal("enemy-jungler-appeared", saved.Id);
        Assert.Equal(PlayerPosition.Jungle, saved.Position);
    }

    [Fact]
    public async Task PackOfVisionRulesAlone_IsNotTreatedAsEmpty()
    {
        var editor = CreateEditor();
        var pack = TestMapControlPackFactory.Create("map-control.vision-only") with
        {
            Rules = [],
            VisionRules =
            [
                new MapVisionRule(
                    "enemy-mid-disappeared",
                    PlayerPosition.Middle,
                    MapVisionTransition.Disappeared,
                    MapObjectiveReminderPriority.Caution,
                    "Вражеский мидер пропал из вида.",
                    "Опасность создаёт незамеченный уход.",
                    "Отойдите под обзор, пока он не найдётся.")
            ]
        };

        await editor.LoadAsync(pack, LocalCoachPackState.Enabled, createCopy: false);

        // Оба встроенных набора наблюдения устроены именно так: ни одного
        // правила по объектам, ни одного напоминания — и это не пустой набор.
        Assert.True(editor.Content.HasContent);
        Assert.True(editor.CanNavigateToStep5);
    }

    [Fact]
    public async Task TwoVisionRulesOnOnePair_BlockVerification()
    {
        var editor = CreateEditor();
        await editor.BeginNewAsync();
        CompleteMainInformation(editor);
        Assert.True(editor.MoveNext());
        Assert.True(editor.MoveNext());
        AddVisionRule(editor.Content, PlayerPosition.Jungle, MapVisionTransition.Appeared);
        AddVisionRule(editor.Content, PlayerPosition.Jungle, MapVisionTransition.Appeared);

        Assert.True(editor.MoveNext());

        // Сработает только первое правило пары. Второе — молчаливо мёртвый текст,
        // и узнать об этом в бою нельзя, поэтому это ошибка, а не предупреждение.
        Assert.False(editor.Verification.CanContinue);
        Assert.Contains(
            editor.Verification.Issues,
            issue => issue.Message.Contains("JUNGLE · появился на карте", StringComparison.Ordinal));
        Assert.Equal(2, editor.Verification.VisionRuleCount);
    }

    [Fact]
    public async Task DistinctVisionPairs_PassVerification()
    {
        var editor = CreateEditor();
        await editor.BeginNewAsync();
        CompleteMainInformation(editor);
        Assert.True(editor.MoveNext());
        Assert.True(editor.MoveNext());
        AddVisionRule(editor.Content, PlayerPosition.Jungle, MapVisionTransition.Appeared);
        AddVisionRule(editor.Content, PlayerPosition.Jungle, MapVisionTransition.Disappeared);

        Assert.True(editor.MoveNext());

        // Одна роль и два разных события — это два разных условия, а не двойник.
        Assert.True(editor.Verification.CanContinue);
    }

    private static void AddVisionRule(
        MapControlContentViewModel content,
        PlayerPosition position,
        MapVisionTransition transition)
    {
        content.VisionRules.BeginAdd();
        var editor = content.VisionRules.Editor;
        editor.SelectedPosition = position;
        editor.SelectedTransition = transition;
        editor.Message = "Соперник изменил видимость на карте.";
        editor.WhyNow = "Это самая скоропортящаяся информация в игре.";
        editor.NextGoal = "Решите, что делать с этим окном.";
        Assert.True(content.VisionRules.SaveEditor());
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
