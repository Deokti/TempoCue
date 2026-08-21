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
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            new TestChampionCatalog(ChampionCatalogLoadResult.Unavailable()),
            new ActiveMatchVisionTestSupport.TestPortraitReferenceLoader(),
            sink,
            LocalizationService.Instance);

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
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
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var loader = new ActiveMatchVisionTestSupport.DeferredCountingPortraitReferenceLoader();
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            ActiveMatchVisionTestSupport.CreateCatalog(),
            loader,
            sink,
            LocalizationService.Instance);

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate(PlayerPosition.Support));
        Assert.Equal(2, loader.CallCount);

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate(PlayerPosition.Middle));

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
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
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
            new ActiveMatchVisionTestSupport.TestPortraitReferenceLoader(),
            sink,
            LocalizationService.Instance);

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
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
    public async Task TemporaryCatalogFailure_RetriesPreparationOnNextMatchUpdate()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            new FlakyChampionCatalog(),
            new ActiveMatchVisionTestSupport.TestPortraitReferenceLoader(),
            sink,
            LocalizationService.Instance,
            observationSource: null,
            observationTimeout: null,
            referenceRetryInterval: TimeSpan.Zero);

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
        // Первая публикация — пустые эталоны при старте, вторая — итог неудачной попытки.
        await sink.WaitForPublicationsAsync(2);
        Assert.All(
            coordinator.DiagnosticsSnapshot.Participants,
            participant => Assert.Equal(
                ChampionPortraitReferenceStatus.Unavailable,
                participant.ReferenceStatus));

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());

        var references = await sink.WaitForReferencesAsync(2);
        Assert.Equal(2, references.Count);
        Assert.All(
            coordinator.DiagnosticsSnapshot.Participants,
            participant => Assert.Equal(
                ChampionPortraitReferenceStatus.Ready,
                participant.ReferenceStatus));
    }

    [Fact]
    public async Task MatchUpdate_MapsChaosPlayerAndParticipantsToRelativeAffiliations()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var localization = LocalizationService.Instance;
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            ActiveMatchVisionTestSupport.CreateCatalog(),
            new ActiveMatchVisionTestSupport.TestPortraitReferenceLoader(),
            sink,
            localization);

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
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

    private sealed class FlakyChampionCatalog : IChampionCatalog
    {
        private int _callCount;

        public Task<ChampionCatalogLoadResult> LoadAsync(
            string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Interlocked.Increment(ref _callCount) == 1
                ? ChampionCatalogLoadResult.Unavailable()
                : new ChampionCatalogLoadResult(
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
    }
}
