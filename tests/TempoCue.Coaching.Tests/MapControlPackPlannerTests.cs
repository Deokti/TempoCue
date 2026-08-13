using TempoCue.Core;

namespace TempoCue.Coaching.Tests;

public sealed class MapControlPackPlannerTests
{
    [Fact]
    public void Build_CombinesObjectiveRulesAndTimedRemindersChronologically()
    {
        var pack = MapControlPackTestData.Create() with
        {
            Rules =
            [
                new MapObjectiveReminderRule(
                    "dragon",
                    MapObjectiveKind.ElementalDragon,
                    MapObjectiveReminderTrigger.BeforeInitialSpawn,
                    TimeSpan.FromSeconds(30),
                    MapObjectiveReminderRecurrence.OncePerMatch,
                    MapObjectiveReminderPriority.Important,
                    "Подготовьтесь к дракону.",
                    "Объект скоро появится.",
                    "Поставить обзор.")
            ],
            TimedReminders =
            [
                new MapControlTimedReminder
                {
                    Id = "river",
                    GameTime = TimeSpan.FromMinutes(3),
                    Priority = MapObjectiveReminderPriority.Caution,
                    Title = "Проверьте реку",
                    Message = "Не отдавайте обзор бесплатно.",
                    WhyNow = "Скоро начнётся борьба за реку.",
                    NextGoal = "Поставить вард."
                }
            ]
        };

        var result = new MapControlPackPlanner(pack).Build([]);

        Assert.Collection(
            result,
            timed =>
            {
                Assert.Equal(MapControlScheduledAdviceKind.TimedReminder, timed.Kind);
                Assert.Equal(TimeSpan.FromMinutes(3), timed.TriggerTime);
                Assert.Equal("Проверьте реку", timed.Title);
            },
            objective =>
            {
                Assert.Equal(MapControlScheduledAdviceKind.ObjectiveRule, objective.Kind);
                Assert.Equal(TimeSpan.FromMinutes(4.5), objective.TriggerTime);
                Assert.Equal(MapObjectiveKind.ElementalDragon, objective.Objective);
                Assert.Equal($"{pack.Id}:objective:dragon:initial", objective.Id);
            });
    }

    [Fact]
    public void BuildPersistentAdvice_FiltersScopesByObjectiveAvailability()
    {
        var pack = MapControlPackTestData.Create() with
        {
            PersistentTips =
            [
                CreatePersistentTip("whole", MapControlPersistentTipScopeKind.WholeMatch),
                CreatePersistentTip("before", MapControlPersistentTipScopeKind.BeforeFirstObjective),
                CreatePersistentTip("available", MapControlPersistentTipScopeKind.WhileAnyObjectiveAvailable),
                CreatePersistentTip(
                    "dragon",
                    MapControlPersistentTipScopeKind.SpecificObjective,
                    MapObjectiveKind.ElementalDragon)
            ]
        };
        var planner = new MapControlPackPlanner(pack);
        var dragonKill = new MapObjectiveKillEvent(
            7,
            MapObjectiveKind.ElementalDragon,
            TimeSpan.FromMinutes(6));

        var beforeSpawn = planner.BuildPersistentAdvice(TimeSpan.FromMinutes(4), []);
        var afterSpawn = planner.BuildPersistentAdvice(TimeSpan.FromMinutes(5), []);
        var afterKill = planner.BuildPersistentAdvice(TimeSpan.FromMinutes(6), [dragonKill]);
        var afterRespawn = planner.BuildPersistentAdvice(TimeSpan.FromMinutes(11), [dragonKill]);

        Assert.Equal(
            ["whole", "before"],
            beforeSpawn.Select(GetSourceTipId));
        Assert.Equal(
            ["whole", "available", "dragon"],
            afterSpawn.Select(GetSourceTipId));
        Assert.Equal(
            ["whole"],
            afterKill.Select(GetSourceTipId));
        Assert.Equal(
            ["whole", "available", "dragon"],
            afterRespawn.Select(GetSourceTipId));
    }

    private static MapControlPersistentTip CreatePersistentTip(
        string id,
        MapControlPersistentTipScopeKind scope,
        MapObjectiveKind? objective = null) => new()
        {
            Id = id,
            Title = $"Подсказка {id}",
            Message = $"Текст подсказки {id}",
            Scope = new MapControlPersistentTipScope(scope, objective)
        };

    private static string GetSourceTipId(MapControlPersistentAdvice advice) =>
        advice.Id[(advice.Id.LastIndexOf(':') + 1)..];
}
