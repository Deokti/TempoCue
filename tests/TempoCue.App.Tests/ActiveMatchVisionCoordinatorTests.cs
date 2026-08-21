using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Проверяет перевод абсолютных команд Riot в стороны относительно игрока.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class ActiveMatchVisionCoordinatorTests
{
    [Fact]
    public async Task UnavailableCatalog_EndsPortraitLoadingWithUnavailableState()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new RecordingChampionReferenceSink();
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            new TestChampionCatalog(ChampionCatalogLoadResult.Unavailable()),
            new TestPortraitReferenceLoader(),
            sink,
            LocalizationService.Instance);

        monitor.Publish(CreateUpdate());
        await sink.WaitForPublicationAsync();

        Assert.All(
            coordinator.DiagnosticsSnapshot.Participants,
            participant => Assert.Equal(
                ChampionPortraitReferenceStatus.Unavailable,
                participant.ReferenceStatus));
    }

    [Fact]
    public async Task PositionUpdate_DoesNotRestartPortraitLoading()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new RecordingChampionReferenceSink();
        var loader = new DeferredCountingPortraitReferenceLoader();
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            CreateCatalog(),
            loader,
            sink,
            LocalizationService.Instance);

        monitor.Publish(CreateUpdate(PlayerPosition.Support));
        Assert.Equal(2, loader.CallCount);

        monitor.Publish(CreateUpdate(PlayerPosition.Middle));

        Assert.Equal(2, loader.CallCount);
        Assert.Equal(
            PlayerPosition.Middle,
            Assert.Single(
                coordinator.DiagnosticsSnapshot.Participants,
                participant => participant.ParticipantId == "active-player").Position);

        loader.Complete();
        await sink.WaitForReferencesAsync(2);
    }

    [Fact]
    public async Task ChampionMissingFromCatalog_EndsOnlyThatParticipantsLoading()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new RecordingChampionReferenceSink();
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            new TestChampionCatalog(new ChampionCatalogLoadResult(
                [
                    new ChampionCatalogEntry(
                        "Pyke",
                        555,
                        "Pyke",
                        new Uri("https://example.test/Pyke.png"))
                ],
                ChampionCatalogSource.Network)),
            new TestPortraitReferenceLoader(),
            sink,
            LocalizationService.Instance);

        monitor.Publish(CreateUpdate());
        await sink.WaitForPublicationAsync();

        var diagnostics = coordinator.DiagnosticsSnapshot;
        Assert.Equal(
            ChampionPortraitReferenceStatus.Ready,
            Assert.Single(
                diagnostics.Participants,
                participant => participant.ChampionName == "Pyke").ReferenceStatus);
        Assert.Equal(
            ChampionPortraitReferenceStatus.Unavailable,
            Assert.Single(
                diagnostics.Participants,
                participant => participant.ChampionName == "Anivia").ReferenceStatus);
    }

    [Fact]
    public async Task MatchUpdate_MapsChaosPlayerAndParticipantsToRelativeAffiliations()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new RecordingChampionReferenceSink();
        var localization = LocalizationService.Instance;
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            CreateCatalog(),
            new TestPortraitReferenceLoader(),
            sink,
            localization);

        monitor.Publish(CreateUpdate());
        var references = await sink.WaitForReferencesAsync(2);

        var diagnostics = coordinator.DiagnosticsSnapshot;
        Assert.Equal(TeamSide.Chaos, diagnostics.ActivePlayerTeam);

        var pyke = Assert.Single(
            diagnostics.Participants,
            participant => participant.ParticipantId == "active-player");
        Assert.Equal(TeamSide.Chaos, pyke.RawTeam);
        Assert.Equal(MinimapMarkerAffiliation.Ally, pyke.RelativeAffiliation);
        Assert.Equal("Pyke", pyke.ChampionId);

        var anivia = Assert.Single(
            diagnostics.Participants,
            participant => participant.ParticipantId == "enemy-player");
        Assert.Equal(TeamSide.Order, anivia.RawTeam);
        Assert.Equal(MinimapMarkerAffiliation.Enemy, anivia.RelativeAffiliation);
        Assert.Equal("Anivia", anivia.ChampionId);

        Assert.Contains(
            references,
            reference => reference.ChampionId == "Pyke" &&
                         reference.Affiliation == MinimapMarkerAffiliation.Ally);
        Assert.Contains(
            references,
            reference => reference.ChampionId == "Anivia" &&
                         reference.Affiliation == MinimapMarkerAffiliation.Enemy);
    }

    private static IChampionCatalog CreateCatalog() => new TestChampionCatalog(
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

    private static ActiveMatchUpdate CreateUpdate(
        PlayerPosition activePlayerPosition = PlayerPosition.Support)
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
                    Level = 2
                }
            ]
        };
        return new ActiveMatchUpdate(
            MatchSnapshotReadStatus.Success,
            new MatchSnapshot(match, [], DateTimeOffset.UtcNow),
            ActiveCoachPackSelection.NotFound());
    }

    private sealed class TestPortraitReferenceLoader : IChampionPortraitReferenceLoader
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

    private sealed class DeferredCountingPortraitReferenceLoader :
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

    private sealed class RecordingChampionReferenceSink : IMinimapChampionReferenceSink
    {
        private readonly TaskCompletionSource<IReadOnlyList<MinimapChampionReference>> _ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _published =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void SetChampionReferences(IReadOnlyList<MinimapChampionReference> references)
        {
            _published.TrySetResult();
            if (references.Count >= 2)
            {
                _ready.TrySetResult(references);
            }
        }

        public Task<IReadOnlyList<MinimapChampionReference>> WaitForReferencesAsync(int count) =>
            _ready.Task.WaitAsync(TimeSpan.FromSeconds(2));

        public Task WaitForPublicationAsync() =>
            _published.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
