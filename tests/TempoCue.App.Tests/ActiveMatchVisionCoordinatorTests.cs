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

    private static ActiveMatchUpdate CreateUpdate()
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
                    Position = PlayerPosition.Support,
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

    private sealed class RecordingChampionReferenceSink : IMinimapChampionReferenceSink
    {
        private readonly TaskCompletionSource<IReadOnlyList<MinimapChampionReference>> _ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void SetChampionReferences(IReadOnlyList<MinimapChampionReference> references)
        {
            if (references.Count >= 2)
            {
                _ready.TrySetResult(references);
            }
        }

        public Task<IReadOnlyList<MinimapChampionReference>> WaitForReferencesAsync(int count) =>
            _ready.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
