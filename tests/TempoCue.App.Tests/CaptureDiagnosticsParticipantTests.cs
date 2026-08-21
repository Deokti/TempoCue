using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Проверяет понятную диагностику каждого участника поверх снимка Vision.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CaptureDiagnosticsParticipantTests
{
    [Fact]
    public void Refresh_ShowsTerminalStateWhenPortraitReferenceIsUnavailable()
    {
        WpfTestHost.Run(() =>
        {
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;

            try
            {
                localization.SetLanguage("ru-RU");
                using var viewModel = new CaptureDiagnosticsViewModel(
                    new TestWindowCaptureSession(),
                    isSupported: true,
                    localization,
                    new TestVisionDiagnosticsSource(new ActiveMatchVisionDiagnosticsSnapshot(
                        TeamSide.Order,
                        [
                            new ActiveMatchVisionParticipantSnapshot(
                                "active-player",
                                "Urgot",
                                null,
                                PlayerPosition.Top,
                                TeamSide.Order,
                                MinimapMarkerAffiliation.Ally,
                                ChampionPortraitReferenceStatus.Unavailable)
                        ])));

                viewModel.Refresh();

                Assert.Equal(
                    "Эталон портрета недоступен",
                    Assert.Single(viewModel.AllyParticipantStatuses).StatusText);
            }
            finally
            {
                localization.SetLanguage(initialLanguage);
            }
        });
    }

    [Fact]
    public void Refresh_SeparatesRiotTeamFromCurrentFrameRecognition()
    {
        WpfTestHost.Run(() =>
        {
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;
            var session = new TestWindowCaptureSession();
            var source = new TestVisionDiagnosticsSource(CreateVisionSnapshot());

            try
            {
                localization.SetLanguage("ru-RU");
                using var viewModel = new CaptureDiagnosticsViewModel(
                    session,
                    isSupported: true,
                    localization,
                    source);
                session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                    state: CaptureSessionState.Capturing,
                    minimapPreview: CreatePreview()));

                viewModel.Refresh();

                Assert.True(viewModel.HasParticipantDiagnostics);
                Assert.Equal("Команда активного игрока Riot: CHAOS", viewModel.ActivePlayerTeamText);

                var ally = Assert.Single(viewModel.AllyParticipantStatuses);
                Assert.Equal("Pyke", ally.ChampionName);
                Assert.Equal("SUP", ally.PositionCode);
                Assert.Equal("Не распознан в текущем кадре", ally.StatusText);
                Assert.Contains("Riot: CHAOS", ally.TeamTraceText, StringComparison.Ordinal);
                Assert.Contains("ожидается: союзник", ally.TeamTraceText, StringComparison.Ordinal);
                Assert.Contains("распознанный контур: —", ally.TeamTraceText, StringComparison.Ordinal);

                var enemy = Assert.Single(viewModel.EnemyParticipantStatuses);
                Assert.Equal("Anivia", enemy.ChampionName);
                Assert.Equal("MID", enemy.PositionCode);
                Assert.Equal(
                    "Распознан с ошибочной стороной: союзник · уверенность 86 %",
                    enemy.StatusText);
                Assert.Contains("Riot: ORDER", enemy.TeamTraceText, StringComparison.Ordinal);
                Assert.Contains("ожидается: противник", enemy.TeamTraceText, StringComparison.Ordinal);
                Assert.Contains("распознанный контур: союзник", enemy.TeamTraceText, StringComparison.Ordinal);
            }
            finally
            {
                localization.SetLanguage(initialLanguage);
            }
        });
    }

    private static ActiveMatchVisionDiagnosticsSnapshot CreateVisionSnapshot() => new(
        TeamSide.Chaos,
        [
            new ActiveMatchVisionParticipantSnapshot(
                "active-player",
                "Pyke",
                "Pyke",
                PlayerPosition.Support,
                TeamSide.Chaos,
                MinimapMarkerAffiliation.Ally),
            new ActiveMatchVisionParticipantSnapshot(
                "enemy-player",
                "Anivia",
                "Anivia",
                PlayerPosition.Middle,
                TeamSide.Order,
                MinimapMarkerAffiliation.Enemy)
        ]);

    private static MinimapPreviewSnapshot CreatePreview() => new(
        X: 0,
        Y: 0,
        RegionWidth: 320,
        RegionHeight: 320,
        ImageWidth: 320,
        ImageHeight: 320,
        ImageStride: 1280,
        BgraPixels: new byte[320 * 320 * 4],
        CapturedAt: DateTimeOffset.UtcNow,
        Corner: MinimapScreenCorner.BottomRight,
        DetectionConfidence: 1)
    {
        ChampionMarkerCandidates =
        [
            new MinimapChampionMarkerCandidate(
                100,
                100,
                17,
                17,
                MinimapMarkerAffiliation.Ally,
                0.9,
                "Anivia",
                0.86)
        ]
    };

    private sealed class TestVisionDiagnosticsSource(
        ActiveMatchVisionDiagnosticsSnapshot snapshot) : IActiveMatchVisionDiagnosticsSource
    {
        public ActiveMatchVisionDiagnosticsSnapshot DiagnosticsSnapshot { get; } = snapshot;
    }
}
