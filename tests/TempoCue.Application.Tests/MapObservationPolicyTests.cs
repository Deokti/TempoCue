using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class MapObservationPolicyTests
{
    [Fact]
    public void IsWanted_ReturnsTrueWhenMatchRunsAndVisionRuleIsEnabled()
    {
        var update = CreateUpdate(
            MatchSnapshotReadStatus.Success,
            CreatePlan(CreateRule()));

        Assert.True(MapObservationPolicy.IsWanted(update));
    }

    [Fact]
    public void IsWanted_ReturnsFalseWhenNoVisionRuleRemains()
    {
        // Ради чего всё и затеяно: без правил наблюдения захват и разбор кадров
        // тратили бы процессор на результат, который некому слушать.
        var update = CreateUpdate(MatchSnapshotReadStatus.Success, CreatePlan());

        Assert.False(MapObservationPolicy.IsWanted(update));
    }

    [Fact]
    public void IsWanted_ReturnsFalseWhileMatchIsStillLoading()
    {
        var update = CreateUpdate(
            MatchSnapshotReadStatus.GameLoading,
            CreatePlan(CreateRule()));

        Assert.False(MapObservationPolicy.IsWanted(update));
    }

    [Fact]
    public void IsWanted_ReturnsFalseWithoutPlan()
    {
        // План отсутствует до первого удачного чтения матча.
        Assert.False(MapObservationPolicy.IsWanted(
            CreateUpdate(MatchSnapshotReadStatus.Success, plan: null)));
        Assert.False(MapObservationPolicy.IsWanted(null));
    }

    [Fact]
    public void IsSwitchedOffDuringMatch_SeparatesOwnChoiceFromAbsentMatch()
    {
        // Пустая диагностика в матче — выбор игрока, и это надо объяснить.
        Assert.True(MapObservationPolicy.IsSwitchedOffDuringMatch(
            CreateUpdate(MatchSnapshotReadStatus.Success, CreatePlan())));

        // Вне матча объяснять нечего: захвата нет по совсем другой причине.
        Assert.False(MapObservationPolicy.IsSwitchedOffDuringMatch(
            CreateUpdate(MatchSnapshotReadStatus.NoActiveMatch, CreatePlan())));
        Assert.False(MapObservationPolicy.IsSwitchedOffDuringMatch(
            CreateUpdate(MatchSnapshotReadStatus.Success, CreatePlan(CreateRule()))));
    }

    private static ActiveMatchUpdate CreateUpdate(
        MatchSnapshotReadStatus status,
        ActiveMapControlPlan? plan) => new(
        status,
        Snapshot: null,
        CoachPackSelection: null,
        plan);

    private static ActiveMapControlPlan CreatePlan(params MapVisionRule[] rules) =>
        new([], [], [], rules);

    private static MapVisionRule CreateRule() => new(
        "enemy-jungler-appeared",
        PlayerPosition.Jungle,
        MapVisionTransition.Appeared,
        MapObjectiveReminderPriority.Important,
        "Сообщение",
        "Почему сейчас",
        "Что дальше");
}
