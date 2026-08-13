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
}
