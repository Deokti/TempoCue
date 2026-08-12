using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MapControlContentViewModelTests
{
    [Fact]
    public void AddDragonRule_SavesValidatedRuleAndUnlocksContent()
    {
        var content = CreateContent();

        content.BeginAddRule(MapObjectiveKind.ElementalDragon);
        FillRequiredAdvice(content.Editor);

        Assert.True(content.Editor.CanSave);
        Assert.True(content.SaveEditor());
        var rule = Assert.Single(content.Rules);
        Assert.Equal(MapObjectiveKind.ElementalDragon, rule.Rule.Objective);
        Assert.Equal(MapObjectiveReminderTrigger.BeforeInitialSpawn, rule.Rule.Trigger);
        Assert.Equal(TimeSpan.FromSeconds(15), rule.Rule.LeadTime);
        Assert.True(content.IsComplete);
    }

    [Fact]
    public void VoidGrubs_DisablesRespawnTrigger()
    {
        var content = CreateContent();

        content.BeginAddRule(MapObjectiveKind.VoidGrubs);

        var respawn = content.Editor.TriggerOptions.Single(
            option => option.Value == MapObjectiveReminderTrigger.BeforeRespawn);
        Assert.False(respawn.IsEnabled);
        content.Editor.SelectedTrigger = MapObjectiveReminderTrigger.BeforeRespawn;
        Assert.Equal(
            MapObjectiveReminderTrigger.BeforeInitialSpawn,
            content.Editor.SelectedTrigger);
    }

    [Fact]
    public void DragonInitialTrigger_AllowsEveryAvailableCycle()
    {
        var content = CreateContent();

        content.BeginAddRule(MapObjectiveKind.ElementalDragon);
        content.Editor.SelectedRecurrence = MapObjectiveReminderRecurrence.EveryCycle;

        Assert.True(content.Editor.CanRepeat);
        Assert.Equal(
            MapObjectiveReminderRecurrence.EveryCycle,
            content.Editor.SelectedRecurrence);
        Assert.True(content.Editor.RecurrenceOptions.Single(
            option => option.Value == MapObjectiveReminderRecurrence.EveryCycle).IsEnabled);
    }

    [Fact]
    public void Rules_CanBeDuplicatedReorderedDisabledAndDeleted()
    {
        var content = CreateContent();
        AddRule(content, MapObjectiveKind.ElementalDragon, "Подготовьтесь к дракону.");
        AddRule(content, MapObjectiveKind.BaronNashor, "Подготовьтесь к Барону.");
        var dragon = content.Rules[0];

        Assert.True(content.DuplicateRule(dragon));
        Assert.Equal(3, content.RuleCount);
        var copy = content.Rules[1];
        Assert.NotEqual(dragon.Id, copy.Id);
        copy.IsEnabled = false;
        Assert.False(copy.Rule.IsEnabled);
        Assert.True(content.MoveRule(copy, 1));
        Assert.Same(copy, content.Rules[2]);
        Assert.True(content.DeleteRule(copy));
        Assert.Equal(2, content.RuleCount);
    }

    [Fact]
    public void PersistentTips_CanBeCreatedEditedDuplicatedAndReordered()
    {
        var content = CreateContent();
        content.ShowPersistentTips();
        content.PersistentTips.BeginAdd();
        content.PersistentTips.Editor.Title = "Не отдавайте реку без обзора";
        content.PersistentTips.Editor.Message =
            "Заранее поставьте обзор и оставьте путь для безопасного отхода.";
        content.PersistentTips.Editor.SelectedScope = new(
            MapControlPersistentTipScopeKind.SpecificObjective,
            MapObjectiveKind.BaronNashor);

        Assert.True(content.PersistentTips.SaveEditor());
        var original = Assert.Single(content.PersistentTips.Tips);
        Assert.Equal(MapObjectiveKind.BaronNashor, original.Scope.Objective);
        Assert.Equal(1, content.PersistentTips.Count);
        Assert.Contains("1", content.PersistentTipsTabLabel, StringComparison.Ordinal);

        var copy = content.PersistentTips.Duplicate(original);
        Assert.Equal(2, content.PersistentTips.Count);
        Assert.NotEqual(original.Id, copy.Id);
        Assert.True(content.PersistentTips.Move(copy, -1));
        Assert.Same(copy, content.PersistentTips.Tips[0]);

        content.PersistentTips.BeginEdit(original);
        content.PersistentTips.Editor.Message =
            "Перед входом в реку обновите обзор и проверьте пути отхода.";
        Assert.True(content.PersistentTips.SaveEditor());
        Assert.Contains("обновите обзор", original.Message, StringComparison.Ordinal);
        Assert.True(content.PersistentTips.Delete(copy));
        Assert.Single(content.PersistentTips.Snapshot);
    }

    [Fact]
    public void TimedReminders_AreSavedOnceAndSortedByGameTime()
    {
        var content = CreateContent();
        content.ShowTimedReminders();

        AddTimedReminder(content, TimeSpan.FromMinutes(12), "Обнови обзор");
        AddTimedReminder(content, TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(15), "Ранний ганк");

        Assert.True(content.IsTimedRemindersSection);
        Assert.Equal(2, content.TimedReminders.Count);
        Assert.Equal("03:15", content.TimedReminders.Reminders[0].GameTimeLabel);
        Assert.Equal("Ранний ганк", content.TimedReminders.Reminders[0].Title);
        Assert.Equal("12:00", content.TimedReminders.Reminders[1].GameTimeLabel);
        Assert.Contains("2", content.TimedRemindersTabLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateNewTimedReminder_SavesOriginalAndOpensIndependentCopy()
    {
        var content = CreateContent();
        content.TimedReminders.BeginAdd();
        FillTimedReminder(content.TimedReminders.Editor, "Ранний обзор реки");

        Assert.True(content.TimedReminders.DuplicateEditor());

        Assert.Equal(2, content.TimedReminders.Count);
        var original = content.TimedReminders.Reminders[0];
        var duplicate = content.TimedReminders.Reminders[1];
        Assert.True(content.TimedReminders.Editor.IsOpen);
        Assert.NotEqual(original.Id, duplicate.Id);
        Assert.Equal(duplicate.Id, content.TimedReminders.Editor.EditingReminderId);
        Assert.StartsWith(original.Title, duplicate.Title, StringComparison.Ordinal);
        Assert.Equal(duplicate.Title, content.TimedReminders.Editor.Title);
    }

    [Fact]
    public void TimedReminderGameTimeInput_AcceptsMinutesOrMmss()
    {
        var editor = new MapControlTimedReminderEditorViewModel(LocalizationService.Instance);
        editor.BeginNew();

        editor.GameTimeInput = "30";
        Assert.Equal(TimeSpan.FromMinutes(30), editor.GameTime);
        Assert.Equal("30:00", editor.GameTimeInput);

        editor.GameTimeInput = "1330";
        Assert.Equal(TimeSpan.FromMinutes(13) + TimeSpan.FromSeconds(30), editor.GameTime);

        editor.GameTimeInput = "1347";
        Assert.Equal(TimeSpan.FromMinutes(13) + TimeSpan.FromSeconds(30), editor.GameTime);
    }

    [Fact]
    public void ContentStep_AllowsAnySingleCompletedContentType()
    {
        var timedContent = CreateContent();
        AddTimedReminder(timedContent, TimeSpan.FromMinutes(10), "Проверь реку");
        Assert.True(timedContent.IsComplete);

        var persistentContent = CreateContent();
        persistentContent.PersistentTips.BeginAdd();
        persistentContent.PersistentTips.Editor.Title = "Сохраняй обзор";
        persistentContent.PersistentTips.Editor.Message =
            "Проверяй доступность обзора перед выходом к нейтральному объекту.";
        Assert.True(persistentContent.PersistentTips.SaveEditor());
        Assert.True(persistentContent.IsComplete);
    }

    private static MapControlContentViewModel CreateContent()
    {
        var content = new MapControlContentViewModel(LocalizationService.Instance);
        content.SetPatch("16.15");
        return content;
    }

    private static void AddRule(
        MapControlContentViewModel content,
        MapObjectiveKind objective,
        string message)
    {
        content.BeginAddRule(objective);
        FillRequiredAdvice(content.Editor, message);
        Assert.True(content.SaveEditor());
    }

    private static void FillRequiredAdvice(
        MapControlObjectiveRuleEditorViewModel editor,
        string message = "Освободите линию и поставьте обзор.")
    {
        editor.Message = message;
        editor.WhyNow = "До появления объекта осталось мало времени.";
        editor.NextGoal = "Подойти к реке вместе с командой.";
    }

    private static void AddTimedReminder(
        MapControlContentViewModel content,
        TimeSpan gameTime,
        string title)
    {
        content.TimedReminders.BeginAdd();
        while (content.TimedReminders.Editor.GameTime < gameTime)
        {
            content.TimedReminders.Editor.IncreaseGameTime();
        }

        FillTimedReminder(content.TimedReminders.Editor, title);
        Assert.True(content.TimedReminders.SaveEditor());
    }

    private static void FillTimedReminder(
        MapControlTimedReminderEditorViewModel editor,
        string title)
    {
        editor.Title = title;
        editor.Message = "Не уходи далеко от безопасной позиции.";
        editor.WhyNow = "Сейчас повышается риск неожиданной атаки.";
        editor.NextGoal = "Дождись обзора и информации о сопернике.";
    }
}
