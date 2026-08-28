using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class SelectActiveMapControlPacksUseCaseTests
{
    [Fact]
    public async Task SelectAsync_UsesOnlyEnabledPacksForPlayerRoleAndSide()
    {
        var store = new TestMapControlPackStore();
        store.Add(
            CreateTimedPack("active", MapControlRole.Top, MapControlTeamSide.Blue),
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.CreatedByUser);
        store.Add(
            CreateTimedPack("disabled", MapControlRole.Top, MapControlTeamSide.Blue),
            LocalCoachPackState.Disabled,
            LocalCoachPackOrigin.CreatedByUser);
        store.Add(
            CreateTimedPack("wrong-role", MapControlRole.Support, MapControlTeamSide.Blue),
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.CreatedByUser);
        store.Add(
            CreateTimedPack("wrong-side", MapControlRole.Top, MapControlTeamSide.Red),
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));

        var result = await useCase.SelectAsync(CreateSnapshot());

        Assert.Equal(["active"], result.ActivePackNames);
        var advice = Assert.Single(result.ScheduledAdvice);
        Assert.Equal("active:timed:timed", advice.Id);
    }

    [Fact]
    public async Task SelectAsync_AppliesAllRolesPackWhenPlayerRoleIsUnknown()
    {
        var store = new TestMapControlPackStore();
        var pack = CreateTimedPack(
            "all-roles",
            MapControlRole.Top,
            MapControlTeamSide.Any) with
        {
            Applicability = new MapControlApplicability
            {
                Roles = MapControlRoleValues.All,
                TeamSide = MapControlTeamSide.Any
            }
        };
        store.Add(pack, LocalCoachPackState.Enabled, LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));

        var result = await useCase.SelectAsync(CreateSnapshot(
            position: PlayerPosition.Unknown));

        Assert.Equal(["all-roles"], result.ActivePackNames);
        Assert.Single(result.ScheduledAdvice);
    }

    [Fact]
    public async Task SelectAsync_DoesNotGuessRestrictedRoleWhenPlayerRoleIsUnknown()
    {
        var store = new TestMapControlPackStore();
        store.Add(
            CreateTimedPack("top-only", MapControlRole.Top, MapControlTeamSide.Any),
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));

        var result = await useCase.SelectAsync(CreateSnapshot(
            position: PlayerPosition.Unknown));

        Assert.Empty(result.ScheduledAdvice);
    }

    [Fact]
    public async Task SelectAsync_BuildsDragonRespawnFromConfirmedNonElderKill()
    {
        var store = new TestMapControlPackStore();
        var pack = TestMapControlPackFactory.Create("respawn") with
        {
            Name = "Respawn",
            Rules =
            [
                new MapObjectiveReminderRule(
                    "dragon-respawn",
                    MapObjectiveKind.ElementalDragon,
                    MapObjectiveReminderTrigger.BeforeRespawn,
                    TimeSpan.FromMinutes(1),
                    MapObjectiveReminderRecurrence.EveryCycle,
                    MapObjectiveReminderPriority.Important,
                    "Идите к дракону.",
                    "До возрождения минута.",
                    "Занять реку.")
            ]
        };
        store.Add(pack, LocalCoachPackState.Enabled, LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));
        var snapshot = CreateSnapshot(
        [
            new MatchEvent
            {
                EventId = 4,
                Kind = MatchEventKind.DragonKill,
                GameTimeSeconds = 420,
                DragonType = DragonType.Fire
            },
            new MatchEvent
            {
                EventId = 9,
                Kind = MatchEventKind.DragonKill,
                GameTimeSeconds = 1200,
                DragonType = DragonType.Elder
            }
        ]);

        var result = await useCase.SelectAsync(snapshot);

        var advice = Assert.Single(result.ScheduledAdvice);
        Assert.Equal(TimeSpan.FromMinutes(11), advice.TriggerTime);
        Assert.Equal(TimeSpan.FromMinutes(12), advice.TargetTime);
        Assert.Contains(":event:4", advice.Id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalidate_ReloadsEnabledMapControlPacks()
    {
        var store = new TestMapControlPackStore();
        var first = CreateTimedPack("first", MapControlRole.Top, MapControlTeamSide.Any);
        store.Add(first, LocalCoachPackState.Enabled, LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));

        var initial = await useCase.SelectAsync(CreateSnapshot());
        await store.SetStateAsync(first.Id, LocalCoachPackState.Disabled);
        var cached = await useCase.SelectAsync(CreateSnapshot());
        useCase.Invalidate();
        var refreshed = await useCase.SelectAsync(CreateSnapshot());

        Assert.Single(initial.ScheduledAdvice);
        Assert.Single(cached.ScheduledAdvice);
        Assert.Empty(refreshed.ScheduledAdvice);
    }

    [Fact]
    public async Task SelectAsync_UsesBuiltInPackByDefaultAndRespectsSavedDisabledState()
    {
        var store = new TestMapControlPackStore();
        var builtInPack = CreateTimedPack(
            "built-in",
            MapControlRole.Top,
            MapControlTeamSide.Any);
        store.Add(
            builtInPack with { Name = "conflicting local pack" },
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.Imported);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store),
            [builtInPack]);

        var initial = await useCase.SelectAsync(CreateSnapshot());
        await store.SetStateAsync(builtInPack.Id, LocalCoachPackState.Disabled);
        useCase.Invalidate();
        var disabled = await useCase.SelectAsync(CreateSnapshot());

        Assert.Equal([builtInPack.Name], initial.ActivePackNames);
        Assert.Single(initial.ScheduledAdvice);
        Assert.Empty(disabled.ActivePackNames);
        Assert.Empty(disabled.ScheduledAdvice);
    }

    [Fact]
    public async Task SelectAsync_ReusesPlanUntilRelevantObjectiveEventsChange()
    {
        var store = new TestMapControlPackStore();
        store.Add(
            CreateTimedPack("cached", MapControlRole.Top, MapControlTeamSide.Any),
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));
        var snapshot = CreateSnapshot();

        var first = await useCase.SelectAsync(snapshot);
        var cached = await useCase.SelectAsync(snapshot);
        var afterEvent = await useCase.SelectAsync(CreateSnapshot(
        [
            new MatchEvent
            {
                EventId = 1,
                Kind = MatchEventKind.DragonKill,
                DragonType = DragonType.Fire,
                GameTimeSeconds = 300
            }
        ]));

        Assert.Same(first, cached);
        Assert.NotSame(first, afterEvent);
    }

    [Fact]
    public async Task SelectAsync_IncludesOnlyCurrentlyApplicablePersistentAdvice()
    {
        var store = new TestMapControlPackStore();
        var pack = CreateTimedPack("persistent", MapControlRole.Top, MapControlTeamSide.Any) with
        {
            PersistentTips =
            [
                new MapControlPersistentTip
                {
                    Id = "whole",
                    Title = "Весь матч",
                    Message = "Следите за рекой.",
                    Scope = new MapControlPersistentTipScope(
                        MapControlPersistentTipScopeKind.WholeMatch)
                },
                new MapControlPersistentTip
                {
                    Id = "dragon",
                    Title = "Дракон доступен",
                    Message = "Займите нижнюю реку.",
                    Scope = new MapControlPersistentTipScope(
                        MapControlPersistentTipScopeKind.SpecificObjective,
                        MapObjectiveKind.ElementalDragon)
                }
            ]
        };
        store.Add(pack, LocalCoachPackState.Enabled, LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));

        var beforeDragon = await useCase.SelectAsync(CreateSnapshot(gameTimeSeconds: 180));
        var afterDragon = await useCase.SelectAsync(CreateSnapshot(gameTimeSeconds: 300));

        Assert.Single(beforeDragon.AvailablePersistentAdvice);
        Assert.Equal(2, afterDragon.AvailablePersistentAdvice.Count);
        Assert.Contains(
            afterDragon.AvailablePersistentAdvice,
            advice => advice.Id.EndsWith(":dragon", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SelectAsync_ReturnsVisionRulesOfEnabledPacks()
    {
        var store = new TestMapControlPackStore();
        store.Add(CreateVisionPack("vision"), LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));

        var result = await useCase.SelectAsync(CreateSnapshot());

        var rule = Assert.Single(result.AvailableVisionRules);
        Assert.Equal(PlayerPosition.Jungle, rule.Position);
    }

    [Fact]
    public async Task SelectAsync_DropsVisionRulesOfDisabledPacks()
    {
        var store = new TestMapControlPackStore();
        store.Add(CreateVisionPack("vision"), LocalCoachPackState.Disabled,
            LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));

        var result = await useCase.SelectAsync(CreateSnapshot());

        // Выключенный набор — выключенные уведомления. Это то же самое действие,
        // которым игрок отключает драконов.
        Assert.Empty(result.AvailableVisionRules);
    }

    [Fact]
    public async Task SelectAsync_DropsVisionRuleDisabledInsideAnEnabledPack()
    {
        var store = new TestMapControlPackStore();
        var pack = CreateVisionPack("vision");
        store.Add(
            pack with
            {
                VisionRules = [pack.VisionRules[0] with { IsEnabled = false }]
            },
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.CreatedByUser);
        var useCase = new SelectActiveMapControlPacksUseCase(
            new LoadMapControlPackLibraryUseCase(store));

        var result = await useCase.SelectAsync(CreateSnapshot());

        // Отключить можно и одно правило, не трогая остальной набор.
        Assert.Empty(result.AvailableVisionRules);
    }

    private static MapControlPack CreateVisionPack(string id) =>
        TestMapControlPackFactory.Create(id) with
        {
            Name = id,
            Applicability = new MapControlApplicability
            {
                Roles = MapControlRoleValues.All,
                TeamSide = MapControlTeamSide.Any
            },
            Rules = [],
            TimedReminders = [],
            VisionRules =
            [
                new MapVisionRule(
                    "enemy-jungler-appeared",
                    PlayerPosition.Jungle,
                    MapVisionTransition.Appeared,
                    MapObjectiveReminderPriority.Important,
                    "Сообщение",
                    "Почему сейчас",
                    "Что дальше")
            ]
        };

    private static MapControlPack CreateTimedPack(
        string id,
        MapControlRole role,
        MapControlTeamSide side) => TestMapControlPackFactory.Create(id) with
        {
            Name = id,
            Applicability = new MapControlApplicability
            {
                Roles = [role],
                TeamSide = side
            },
            Rules = [],
            TimedReminders =
            [
                new MapControlTimedReminder
                {
                    Id = "timed",
                    GameTime = TimeSpan.FromMinutes(3),
                    Priority = MapObjectiveReminderPriority.Information,
                    Title = "Напоминание",
                    Message = "Сообщение",
                    WhyNow = "Причина",
                    NextGoal = "Цель"
                }
            ]
        };

    private static MatchSnapshot CreateSnapshot(
        IReadOnlyList<MatchEvent>? events = null,
        PlayerPosition position = PlayerPosition.Top,
        double gameTimeSeconds = 180) => new(
        new MatchState
        {
            GameTimeSeconds = gameTimeSeconds,
            ActivePlayerId = "player",
            Players =
            [
                new PlayerState
                {
                    ParticipantId = "player",
                    ChampionName = "Urgot",
                    Team = TeamSide.Order,
                    Position = position,
                    Level = 5
                }
            ]
        },
        events ?? [],
        DateTimeOffset.UtcNow);
}
