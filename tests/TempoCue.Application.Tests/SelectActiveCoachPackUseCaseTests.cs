using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class SelectActiveCoachPackUseCaseTests
{
    [Fact]
    public async Task SelectAsync_PrefersSingleEnabledUserPackOverBuiltInPack()
    {
        var builtIn = CreatePack("built-in", "Встроенный", "Встроенный совет");
        var userPack = CreatePack("user", "Пользовательский", "Пользовательский совет");
        var store = new StubCoachPackStore(Snapshot(
            new LocalCoachPackEntry(
                userPack,
                LocalCoachPackState.Enabled,
                DateTimeOffset.UtcNow)));
        var useCase = CreateUseCase(builtIn, store);

        var selection = await useCase.SelectAsync(CreateMatch());

        Assert.Equal(ActiveCoachPackSelectionState.Selected, selection.State);
        Assert.Same(userPack, selection.Pack);
        Assert.Equal("Пользовательский совет", selection.Advice?.Message);
    }

    [Fact]
    public async Task SelectAsync_UsesBuiltInPackWhenUserPackIsDisabled()
    {
        var builtIn = CreatePack("built-in", "Встроенный", "Встроенный совет");
        var disabledPack = CreatePack("user", "Выключенный", "Другой совет");
        var store = new StubCoachPackStore(Snapshot(
            new LocalCoachPackEntry(
                disabledPack,
                LocalCoachPackState.Disabled,
                DateTimeOffset.UtcNow)));
        var useCase = CreateUseCase(builtIn, store);

        var selection = await useCase.SelectAsync(CreateMatch());

        Assert.Equal(ActiveCoachPackSelectionState.Selected, selection.State);
        Assert.Same(builtIn, selection.Pack);
        Assert.Equal("Встроенный совет", selection.Advice?.Message);
    }

    [Fact]
    public async Task SelectAsync_ReturnsNotFoundWhenBuiltInCatalogAndLibraryAreEmpty()
    {
        var store = new StubCoachPackStore(Snapshot());
        var useCase = new SelectActiveCoachPackUseCase(
            [],
            new LoadCoachPackLibraryUseCase(store));

        var selection = await useCase.SelectAsync(CreateMatch());

        Assert.Equal(ActiveCoachPackSelectionState.NotFound, selection.State);
        Assert.Null(selection.Pack);
        Assert.Null(selection.Advice);
    }

    [Fact]
    public async Task SelectAsync_ReturnsConflictForSeveralMatchingUserPacks()
    {
        var first = CreatePack("first", "Альфа", "Первый совет");
        var second = CreatePack("second", "Бета", "Второй совет");
        var store = new StubCoachPackStore(Snapshot(
            new LocalCoachPackEntry(first, LocalCoachPackState.Enabled, DateTimeOffset.UtcNow),
            new LocalCoachPackEntry(second, LocalCoachPackState.Enabled, DateTimeOffset.UtcNow)));
        var useCase = CreateUseCase(
            CreatePack("built-in", "Встроенный", "Встроенный совет"),
            store);

        var selection = await useCase.SelectAsync(CreateMatch());

        Assert.Equal(ActiveCoachPackSelectionState.Conflict, selection.State);
        Assert.Null(selection.Pack);
        Assert.Null(selection.Advice);
        Assert.Equal(["Альфа", "Бета"], selection.ConflictingPackNames);
    }

    [Fact]
    public async Task SelectAsync_RespectsKnownLaneRole()
    {
        var middlePack = CreatePack(
            "middle",
            "Средняя линия",
            "Совет для мида",
            laneRole: "mid");
        var store = new StubCoachPackStore(Snapshot(
            new LocalCoachPackEntry(
                middlePack,
                LocalCoachPackState.Enabled,
                DateTimeOffset.UtcNow)));
        var useCase = CreateUseCase(
            CreatePack("built-in", "Встроенный", "Встроенный совет"),
            store);

        var selection = await useCase.SelectAsync(CreateMatch(PlayerPosition.Top));

        Assert.Equal(ActiveCoachPackSelectionState.Selected, selection.State);
        Assert.Equal("built-in", selection.Pack?.Id);
    }

    [Fact]
    public async Task Invalidate_ReloadsLibraryOnlyForNextSelection()
    {
        var first = CreatePack("first", "Первый", "Первый совет");
        var second = CreatePack("second", "Второй", "Второй совет");
        var store = new StubCoachPackStore(Snapshot(
            new LocalCoachPackEntry(first, LocalCoachPackState.Enabled, DateTimeOffset.UtcNow)));
        var useCase = CreateUseCase(
            CreatePack("built-in", "Встроенный", "Встроенный совет"),
            store);

        var initial = await useCase.SelectAsync(CreateMatch());
        store.Library = Snapshot(
            new LocalCoachPackEntry(second, LocalCoachPackState.Enabled, DateTimeOffset.UtcNow));
        var cached = await useCase.SelectAsync(CreateMatch());
        useCase.Invalidate();
        var refreshed = await useCase.SelectAsync(CreateMatch());

        Assert.Equal("first", initial.Pack?.Id);
        Assert.Equal("first", cached.Pack?.Id);
        Assert.Equal("second", refreshed.Pack?.Id);
        Assert.Equal(2, store.LoadCount);
    }

    [Fact]
    public async Task SelectAsync_CachesExpectedLibraryFailureUntilInvalidated()
    {
        var store = new StubCoachPackStore(Snapshot())
        {
            LoadException = new IOException("Тестовая ошибка")
        };
        var useCase = CreateUseCase(
            CreatePack("built-in", "Встроенный", "Встроенный совет"),
            store);

        var first = await useCase.SelectAsync(CreateMatch());
        var cached = await useCase.SelectAsync(CreateMatch());
        useCase.Invalidate();
        var retried = await useCase.SelectAsync(CreateMatch());

        Assert.Equal(ActiveCoachPackSelectionState.LibraryUnavailable, first.State);
        Assert.Equal(ActiveCoachPackSelectionState.LibraryUnavailable, cached.State);
        Assert.Equal(ActiveCoachPackSelectionState.LibraryUnavailable, retried.State);
        Assert.Equal(2, store.LoadCount);
    }

    private static SelectActiveCoachPackUseCase CreateUseCase(
        CoachPack builtIn,
        ICoachPackStore store) => new(
            [builtIn],
            new LoadCoachPackLibraryUseCase(store));

    private static CoachPackLibrarySnapshot Snapshot(params LocalCoachPackEntry[] entries) =>
        new(
            entries,
            0,
            new Dictionary<string, LocalCoachPackState>(StringComparer.OrdinalIgnoreCase));

    private static CoachPack CreatePack(
        string id,
        string name,
        string message,
        string laneRole = "top") => new()
        {
            SchemaVersion = 2,
            Id = id,
            Name = name,
            Patch = "26.15",
            PlayerChampion = "Urgot",
            OpponentChampion = "Sett",
            LaneRole = laneRole,
            Rules =
            [
                new CoachRuleDefinition
                {
                    Id = $"{id}.rule",
                    PlayerChampion = "Urgot",
                    OpponentChampion = "Sett",
                    MinimumLevel = 1,
                    PowerState = PowerState.Cautious,
                    Priority = 10,
                    Title = name,
                    Message = message,
                    Source = "Тест"
                }
            ]
        };

    private static MatchState CreateMatch(PlayerPosition position = PlayerPosition.Top) => new()
    {
        GameTimeSeconds = 540,
        ActivePlayerId = "player",
        Players =
        [
            new PlayerState
            {
                ParticipantId = "player",
                ChampionName = "Urgot",
                Team = TeamSide.Order,
                Position = position,
                Level = 9
            },
            new PlayerState
            {
                ParticipantId = "opponent",
                ChampionName = "Sett",
                Team = TeamSide.Chaos,
                Position = position,
                Level = 9
            }
        ]
    };

    private sealed class StubCoachPackStore(CoachPackLibrarySnapshot library) : ICoachPackStore
    {
        public CoachPackLibrarySnapshot Library { get; set; } = library;

        public int LoadCount { get; private set; }

        public Exception? LoadException { get; init; }

        public Task<CoachPackLibrarySnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            if (LoadException is not null)
            {
                throw LoadException;
            }

            return Task.FromResult(Library);
        }

        public Task<CoachPackSaveResult> SaveAsync(
            CoachPack pack,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetStateAsync(
            string packId,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
