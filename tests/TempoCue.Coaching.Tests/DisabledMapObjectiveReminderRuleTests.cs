using TempoCue.Core;

namespace TempoCue.Coaching.Tests;

public sealed class DisabledMapObjectiveReminderRuleTests
{
    [Fact]
    public void Build_DoesNotPlanDisabledRule()
    {
        var timing = new MapObjectiveTiming(
            MapObjectiveKind.ElementalDragon,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
        var rule = new MapObjectiveReminderRule(
            "disabled-dragon",
            MapObjectiveKind.ElementalDragon,
            MapObjectiveReminderTrigger.BeforeInitialSpawn,
            TimeSpan.FromSeconds(30),
            MapObjectiveReminderRecurrence.OncePerMatch,
            MapObjectiveReminderPriority.Information,
            "Освободите линию перед драконом.",
            "До появления осталось мало времени.",
            "Подойти к реке вместе с командой.",
            IsEnabled: false);

        var result = new MapObjectiveReminderPlanner().Build(rule, timing, []);

        Assert.Empty(result);
    }
}
