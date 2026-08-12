using TempoCue.Core;

namespace TempoCue.Coaching.Tests;

public sealed class MapObjectiveReminderPlannerTests
{
    private static readonly MapObjectiveTiming DragonTiming = new(
        MapObjectiveKind.ElementalDragon,
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5));

    private readonly MapObjectiveReminderPlanner _planner = new();

    [Fact]
    public void Build_PlansReminderBeforeInitialSpawn()
    {
        var result = _planner.Build(
            CreateRule(
                MapObjectiveReminderTrigger.BeforeInitialSpawn,
                TimeSpan.FromSeconds(45)),
            DragonTiming,
            []);

        var occurrence = Assert.Single(result);
        Assert.Equal(TimeSpan.FromMinutes(4.25), occurrence.TriggerTime);
        Assert.Equal(TimeSpan.FromMinutes(5), occurrence.TargetTime);
        Assert.Equal("rule:initial", occurrence.Id);
        Assert.Null(occurrence.SourceEventId);
    }

    [Fact]
    public void Build_PlansEveryRespawnFromSortedMatchingEvents()
    {
        var result = _planner.Build(
            CreateRule(
                MapObjectiveReminderTrigger.BeforeRespawn,
                TimeSpan.FromSeconds(60),
                MapObjectiveReminderRecurrence.EveryCycle),
            DragonTiming,
            [
                new MapObjectiveKillEvent(12, MapObjectiveKind.ElementalDragon, TimeSpan.FromMinutes(18)),
                new MapObjectiveKillEvent(3, MapObjectiveKind.RiftHerald, TimeSpan.FromMinutes(15)),
                new MapObjectiveKillEvent(5, MapObjectiveKind.ElementalDragon, TimeSpan.FromMinutes(7))
            ]);

        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal(TimeSpan.FromMinutes(11), first.TriggerTime);
                Assert.Equal(TimeSpan.FromMinutes(12), first.TargetTime);
                Assert.Equal(5, first.SourceEventId);
            },
            second =>
            {
                Assert.Equal(TimeSpan.FromMinutes(22), second.TriggerTime);
                Assert.Equal(TimeSpan.FromMinutes(23), second.TargetTime);
                Assert.Equal(12, second.SourceEventId);
            });
    }

    [Fact]
    public void Build_UsesFirstChronologicalKill_ForOncePerMatchRule()
    {
        var result = _planner.Build(
            CreateRule(MapObjectiveReminderTrigger.AfterKill),
            DragonTiming,
            [
                new MapObjectiveKillEvent(8, MapObjectiveKind.ElementalDragon, TimeSpan.FromMinutes(14)),
                new MapObjectiveKillEvent(2, MapObjectiveKind.ElementalDragon, TimeSpan.FromMinutes(8))
            ]);

        var occurrence = Assert.Single(result);
        Assert.Equal(TimeSpan.FromMinutes(8), occurrence.TriggerTime);
        Assert.Equal("rule:event:2", occurrence.Id);
    }

    [Fact]
    public void Build_RejectsDuplicateEventIds()
    {
        var exception = Assert.Throws<InvalidDataException>(() => _planner.Build(
            CreateRule(
                MapObjectiveReminderTrigger.AfterKill,
                recurrence: MapObjectiveReminderRecurrence.EveryCycle),
            DragonTiming,
            [
                new MapObjectiveKillEvent(4, MapObjectiveKind.ElementalDragon, TimeSpan.FromMinutes(8)),
                new MapObjectiveKillEvent(4, MapObjectiveKind.ElementalDragon, TimeSpan.FromMinutes(14))
            ]));

        Assert.Contains("Duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsRespawnRule_ForObjectiveWithoutRespawn()
    {
        var grubsTiming = new MapObjectiveTiming(
            MapObjectiveKind.VoidGrubs,
            TimeSpan.FromMinutes(8),
            null);

        var exception = Assert.Throws<InvalidDataException>(() => _planner.Build(
            CreateRule(
                MapObjectiveReminderTrigger.BeforeRespawn,
                TimeSpan.FromSeconds(30),
                objective: MapObjectiveKind.VoidGrubs),
            grubsTiming,
            []));

        Assert.Contains("does not respawn", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MapObjectiveReminderTrigger.AtInitialSpawn)]
    [InlineData(MapObjectiveReminderTrigger.AfterKill)]
    public void Build_RejectsLeadTime_ForImmediateTrigger(
        MapObjectiveReminderTrigger trigger)
    {
        Assert.Throws<InvalidDataException>(() => _planner.Build(
            CreateRule(trigger, TimeSpan.FromSeconds(10)),
            DragonTiming,
            []));
    }

    private static MapObjectiveReminderRule CreateRule(
        MapObjectiveReminderTrigger trigger,
        TimeSpan? leadTime = null,
        MapObjectiveReminderRecurrence recurrence = MapObjectiveReminderRecurrence.OncePerMatch,
        MapObjectiveKind objective = MapObjectiveKind.ElementalDragon) =>
        new(
            "rule",
            objective,
            trigger,
            leadTime ?? TimeSpan.Zero,
            recurrence,
            MapObjectiveReminderPriority.Caution,
            "Подготовьтесь к объекту",
            "Заранее освободите линию и поставьте обзор.",
            "До появления объекта осталось мало времени.",
            "Подойти к реке вместе с командой.");
}
