using TempoCue.App.Coordinators;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.Tests;

internal static class ActiveMatchVisionTestSupport
{
    public static IChampionCatalog CreateCatalog() => new TestChampionCatalog(
        new ChampionCatalogLoadResult(
            [
                new ChampionCatalogEntry(
                    "Pyke",
                    555,
                    "Pyke",
                    new Uri("https://example.test/Pyke.png")),
                new ChampionCatalogEntry(
                    "Anivia",
                    34,
                    "Anivia",
                    new Uri("https://example.test/Anivia.png"))
            ],
            ChampionCatalogSource.Network));

    public static ActiveMatchUpdate CreateUpdate(
        PlayerPosition activePlayerPosition = PlayerPosition.Support,
        bool enemyIsDead = false)
    {
        var match = new MatchState
        {
            GameTimeSeconds = 120,
            ActivePlayerId = "active-player",
            Players =
            [
                new PlayerState
                {
                    ParticipantId = "active-player",
                    ChampionName = "Pyke",
                    Team = TeamSide.Chaos,
                    Position = activePlayerPosition,
                    Level = 2
                },
                new PlayerState
                {
                    ParticipantId = "enemy-player",
                    ChampionName = "Anivia",
                    Team = TeamSide.Order,
                    Position = PlayerPosition.Middle,
                    Level = 2,
                    IsDead = enemyIsDead
                }
            ]
        };
        return new ActiveMatchUpdate(
            MatchSnapshotReadStatus.Success,
            new MatchSnapshot(match, [], DateTimeOffset.UtcNow),
            ActiveCoachPackSelection.NotFound());
    }

    internal sealed class TestPortraitReferenceLoader : IChampionPortraitReferenceLoader
    {
        public Task<MinimapChampionReference?> LoadAsync(
            ChampionCatalogEntry champion,
            MinimapMarkerAffiliation affiliation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<MinimapChampionReference?>(new(
                champion.Id,
                affiliation,
                new byte[
                    MinimapChampionReference.ImageSide *
                    MinimapChampionReference.ImageSide *
                    MinimapChampionReference.BytesPerPixel]));
        }
    }

    internal sealed class DeferredCountingPortraitReferenceLoader :
        IChampionPortraitReferenceLoader
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<MinimapChampionReference?> LoadAsync(
            ChampionCatalogEntry champion,
            MinimapMarkerAffiliation affiliation,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            await _completion.Task.WaitAsync(cancellationToken);
            return new MinimapChampionReference(
                champion.Id,
                affiliation,
                new byte[
                    MinimapChampionReference.ImageSide *
                    MinimapChampionReference.ImageSide *
                    MinimapChampionReference.BytesPerPixel]);
        }

        public void Complete() => _completion.TrySetResult();
    }

    internal sealed class RecordingChampionReferenceSink : IMinimapChampionReferenceSink
    {
        private readonly object _sync = new();
        private readonly List<(int Count, TaskCompletionSource Completion)> _waiters = [];
        private readonly TaskCompletionSource<IReadOnlyList<MinimapChampionReference>> _ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _published =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _publicationCount;

        public void SetChampionReferences(IReadOnlyList<MinimapChampionReference> references)
        {
            _published.TrySetResult();
            if (references.Count >= 2)
            {
                _ready.TrySetResult(references);
            }

            lock (_sync)
            {
                _publicationCount++;
                for (var index = _waiters.Count - 1; index >= 0; index--)
                {
                    if (_publicationCount >= _waiters[index].Count)
                    {
                        _waiters[index].Completion.TrySetResult();
                        _waiters.RemoveAt(index);
                    }
                }
            }
        }

        public Task<IReadOnlyList<MinimapChampionReference>> WaitForReferencesAsync(int count) =>
            _ready.Task.WaitAsync(TimeSpan.FromSeconds(2));

        public Task WaitForPublicationAsync() =>
            _published.Task.WaitAsync(TimeSpan.FromSeconds(2));

        public Task WaitForPublicationsAsync(int count)
        {
            lock (_sync)
            {
                if (_publicationCount >= count)
                {
                    return Task.CompletedTask;
                }

                var completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((count, completion));
                return completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
            }
        }
    }

    internal sealed class TestObservationSource : IMinimapChampionObservationSource
    {
        public event EventHandler<MinimapChampionObservationEventArgs>? ObservationAvailable;

        public void Publish(
            DateTimeOffset capturedAt,
            params MinimapChampionMarkerCandidate[] candidates) =>
            ObservationAvailable?.Invoke(
                this,
                new MinimapChampionObservationEventArgs(
                    capturedAt,
                    320,
                    320,
                    candidates));
    }
}
