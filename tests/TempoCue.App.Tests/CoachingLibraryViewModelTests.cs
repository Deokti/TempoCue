using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CoachingLibraryViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShowsBuiltInMapControlPackAsProtectedAndEnabledByDefault()
    {
        var coachPackStore = new TestCoachPackStore();
        var mapControlStore = new TestMapControlPackStore();
        var builtInPack = TestMapControlPackFactory.Create("built-in-map-control") with
        {
            Name = "Built-in map control"
        };
        using var viewModel = CreateViewModel(
            coachPackStore,
            mapControlStore,
            [builtInPack]);

        await viewModel.RefreshAsync();

        var card = Assert.Single(viewModel.MapControlCards);
        Assert.Equal(CoachingPackOrigin.BuiltIn, card.Origin);
        Assert.True(card.IsEnabled);
        Assert.False(card.CanEdit);
        Assert.False(card.CanDelete);
        Assert.True(card.CanCreateCopy);
    }

    [Fact]
    public async Task RefreshAsync_SerializesConcurrentLoadsAndKeepsNewestSnapshot()
    {
        var store = new ControlledCoachPackStore();
        using var viewModel = CreateViewModel(store);
        var firstPack = CreatePack("first-pack");
        var secondPack = CreatePack("second-pack");

        var firstRefresh = viewModel.RefreshAsync();
        var secondRefresh = viewModel.RefreshAsync();

        Assert.Equal(1, store.RequestCount);
        store.Complete(0, CreateSnapshot(firstPack));
        await store.SecondRequestStarted.WaitAsync(TimeSpan.FromSeconds(2));
        store.Complete(1, CreateSnapshot(secondPack));

        await Task.WhenAll(firstRefresh, secondRefresh);

        Assert.False(viewModel.IsLibraryLoading);
        Assert.Null(viewModel.FindCard(firstPack.Id));
        Assert.NotNull(viewModel.FindCard(secondPack.Id));
    }

    private static CoachingLibraryViewModel CreateViewModel(
        ICoachPackStore store,
        TestMapControlPackStore? mapControlStore = null,
        IReadOnlyList<MapControlPack>? builtInMapControlPacks = null)
    {
        var localization = LocalizationService.Instance;
        mapControlStore ??= new TestMapControlPackStore();
        return new CoachingLibraryViewModel(
            [],
            localization,
            TestChampionCatalog.Ready(),
            new LoadCoachPackLibraryUseCase(store),
            new SetCoachPackStateUseCase(store),
            new DeleteCoachPackUseCase(store),
            new LoadMapControlPackLibraryUseCase(mapControlStore),
            new SetMapControlPackStateUseCase(mapControlStore),
            new DeleteMapControlPackUseCase(mapControlStore),
            builtInMapControlPacks);
    }

    private static CoachPack CreatePack(string id) =>
        TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Id = id,
            Name = id
        };

    private static CoachPackLibrarySnapshot CreateSnapshot(CoachPack pack) => new(
        [
            new LocalCoachPackEntry(
                pack,
                LocalCoachPackState.Enabled,
                DateTimeOffset.UtcNow,
                LocalCoachPackOrigin.CreatedByUser)
        ],
        0,
        new Dictionary<string, LocalCoachPackState>(StringComparer.OrdinalIgnoreCase)
        {
            [pack.Id] = LocalCoachPackState.Enabled
        });

    private sealed class ControlledCoachPackStore : ICoachPackStore
    {
        private readonly List<TaskCompletionSource<CoachPackLibrarySnapshot>> _requests = [];
        private readonly TaskCompletionSource _secondRequestStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int RequestCount => _requests.Count;

        public Task SecondRequestStarted => _secondRequestStarted.Task;

        public Task<CoachPackLibrarySnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<CoachPackLibrarySnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(completion);
            if (_requests.Count == 2)
            {
                _secondRequestStarted.TrySetResult();
            }

            return completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete(int requestIndex, CoachPackLibrarySnapshot snapshot) =>
            _requests[requestIndex].TrySetResult(snapshot);

        public Task<CoachPackSaveResult> SaveAsync(
            CoachPack pack,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CoachPackSaveResult> ImportAsync(
            CoachPack pack,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetStateAsync(
            string packId,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            string packId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
