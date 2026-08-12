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
}
