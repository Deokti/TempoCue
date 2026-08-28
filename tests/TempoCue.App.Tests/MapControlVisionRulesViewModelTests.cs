using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Проверяет раздел правил наблюдения в редакторе набора контроля карты.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MapControlVisionRulesViewModelTests
{
    [Fact]
    public void SavedRule_KeepsPairAndTexts()
    {
        var rules = CreateRules();

        AddRule(rules, PlayerPosition.Middle, MapVisionTransition.Disappeared);

        var rule = Assert.Single(rules.Rules);
        Assert.Equal(PlayerPosition.Middle, rule.Position);
        Assert.Equal(MapVisionTransition.Disappeared, rule.Transition);
        Assert.Equal("MID · пропал из вида", rule.PairLabel);
        Assert.Single(rules.Snapshot);
    }

    [Fact]
    public void Editor_RequiresOnlyTheAdviceItself()
    {
        var rules = CreateRules();
        rules.BeginAdd();

        Assert.False(rules.Editor.CanSave);

        rules.Editor.Message = "Вражеский мидер пропал из вида.";

        // «Почему сейчас» и «следующая цель» необязательны: в оверлей они не
        // попадают вовсе, и требовать их значило бы заставлять автора писать
        // текст, которого игрок в бою не увидит.
        Assert.True(rules.Editor.CanSave);
        Assert.True(rules.SaveEditor());

        var saved = Assert.Single(rules.Snapshot);
        Assert.Equal(string.Empty, saved.WhyNow);
        Assert.Equal(string.Empty, saved.NextGoal);
    }

    [Fact]
    public void TwoRulesOnOnePair_AreReportedAsDuplicates()
    {
        var rules = CreateRules();
        AddRule(rules, PlayerPosition.Jungle, MapVisionTransition.Appeared);
        AddRule(rules, PlayerPosition.Jungle, MapVisionTransition.Appeared);
        AddRule(rules, PlayerPosition.Top, MapVisionTransition.Appeared);

        // Сработает только первое правило пары, остальные — молчаливо мёртвый
        // текст, и назвать их надо до сохранения, а не в бою.
        var duplicate = Assert.Single(rules.DuplicatePairLabels);
        Assert.Equal("JUNGLE · появился на карте", duplicate);
    }

    [Fact]
    public void AuthorOrder_SurvivesSaveAndMoves()
    {
        var rules = CreateRules();
        AddRule(rules, PlayerPosition.Jungle, MapVisionTransition.Appeared);
        AddRule(rules, PlayerPosition.Middle, MapVisionTransition.Disappeared);

        // Порядок — часть замысла автора: при наблюдении срабатывает первое
        // подошедшее правило, поэтому список не сортируется сам.
        Assert.Equal(
            [PlayerPosition.Jungle, PlayerPosition.Middle],
            rules.Snapshot.Select(static rule => rule.Position));

        Assert.True(rules.Move(rules.Rules[1], -1));

        Assert.Equal(
            [PlayerPosition.Middle, PlayerPosition.Jungle],
            rules.Snapshot.Select(static rule => rule.Position));
    }

    [Fact]
    public void DisabledRule_StaysDisabledAfterEditing()
    {
        var rules = CreateRules();
        rules.Load(
        [
            new MapVisionRule(
                "enemy-jungler-appeared",
                PlayerPosition.Jungle,
                MapVisionTransition.Appeared,
                MapObjectiveReminderPriority.Important,
                "Вражеский лесник виден на карте.",
                "Положение лесника — самая скоропортящаяся информация.",
                "Реши, даёт ли это окно.",
                IsEnabled: false)
        ]);

        rules.BeginEdit(rules.Rules[0]);
        rules.Editor.Message = "Вражеский лесник показался на карте.";
        Assert.True(rules.SaveEditor());

        // Выключателя в редакторе нет, но правка не имеет права включить
        // правило, которое автор выключил.
        Assert.False(rules.Snapshot[0].IsEnabled);
        Assert.Equal("Вражеский лесник показался на карте.", rules.Snapshot[0].Message);
    }

    [Fact]
    public void Preview_NamesBothHalvesOfTheCondition()
    {
        var rules = CreateRules();
        rules.BeginAdd();
        rules.Editor.SelectedPosition = PlayerPosition.Bottom;
        rules.Editor.SelectedTransition = MapVisionTransition.Disappeared;

        // Роли называются так же, как в области применения: TOP, JUNGLE, MID,
        // ADC, SUPPORT — это имена самой игры, и переводить их незачем.
        Assert.Equal(
            "Сработает, когда наблюдение зафиксирует: ADC — пропал из вида",
            rules.Editor.PreviewText);
    }

    [Fact]
    public void PositionOptions_LeaveOutTheUnknownRole()
    {
        var rules = CreateRules();

        // Роль приходит из Live Client Data, и правило «о ком угодно»
        // срабатывало бы на случайном участнике вместо названного.
        Assert.Equal(5, rules.Editor.PositionOptions.Count);
        Assert.DoesNotContain(
            rules.Editor.PositionOptions,
            option => option.Value == PlayerPosition.Unknown);
    }

    private static MapControlVisionRulesViewModel CreateRules()
    {
        LocalizationService.Instance.SetLanguage("ru-RU");
        return new MapControlVisionRulesViewModel(LocalizationService.Instance);
    }

    private static void AddRule(
        MapControlVisionRulesViewModel rules,
        PlayerPosition position,
        MapVisionTransition transition)
    {
        rules.BeginAdd();
        rules.Editor.SelectedPosition = position;
        rules.Editor.SelectedTransition = transition;
        rules.Editor.Message = "Соперник изменил видимость на карте.";
        rules.Editor.WhyNow = "Это самая скоропортящаяся информация в игре.";
        Assert.True(rules.SaveEditor());
    }
}
