using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
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
        RunInRussian(localization =>
        {
            using var viewModel = CreateViewModel(
                localization,
                new ActiveMatchVisionParticipantSnapshot(
                    "active-player",
                    "Urgot",
                    null,
                    PlayerPosition.Top,
                    TeamSide.Order,
                    MinimapMarkerAffiliation.Ally,
                    ChampionPortraitReferenceStatus.Unavailable));

            viewModel.Refresh();

            Assert.Equal(
                "Эталон портрета недоступен",
                Assert.Single(viewModel.AllyParticipantStatuses).StatusText);
        });
    }

    [Fact]
    public void Refresh_ShowsStabilizedVisibilityInsteadOfRawFrameRecognition()
    {
        RunInRussian(localization =>
        {
            var source = new TestVisionDiagnosticsSource(CreateVisionSnapshot());
            using var viewModel = new CaptureDiagnosticsViewModel(
                new TestWindowCaptureSession(),
                isSupported: true,
                localization,
                source);

            viewModel.Refresh();

            Assert.True(viewModel.HasParticipantDiagnostics);
            Assert.Equal("Команда активного игрока Riot: CHAOS", viewModel.ActivePlayerTeamText);

            var ally = Assert.Single(viewModel.AllyParticipantStatuses);
            Assert.Equal("Pyke", ally.ChampionName);
            Assert.Equal("SUP", ally.PositionCode);
            Assert.Equal("Не виден", ally.StatusText);
            Assert.Contains("Riot: CHAOS", ally.TeamTraceText, StringComparison.Ordinal);
            Assert.Contains("ожидается: союзник", ally.TeamTraceText, StringComparison.Ordinal);

            var enemy = Assert.Single(viewModel.EnemyParticipantStatuses);
            Assert.Equal("Anivia", enemy.ChampionName);
            Assert.Equal("MID", enemy.PositionCode);
            Assert.Equal("Виден · уверенность 86 %", enemy.StatusText);
            Assert.Contains("Riot: ORDER", enemy.TeamTraceText, StringComparison.Ordinal);
            Assert.Contains("ожидается: противник", enemy.TeamTraceText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Refresh_DoesNotPresentDeadParticipantAsVisible()
    {
        RunInRussian(localization =>
        {
            using var viewModel = CreateViewModel(
                localization,
                new ActiveMatchVisionParticipantSnapshot(
                    "enemy-player",
                    "Twisted Fate",
                    "TwistedFate",
                    PlayerPosition.Middle,
                    TeamSide.Chaos,
                    MinimapMarkerAffiliation.Enemy,
                    IsDead: true,
                    VisibilityStatus: ParticipantVisibilityStatus.Visible,
                    VisibilityConfidence: 0.91));

            viewModel.Refresh();

            Assert.Equal(
                "Мёртв · наблюдение приостановлено",
                Assert.Single(viewModel.EnemyParticipantStatuses).StatusText);
        });
    }

    [Fact]
    public void Refresh_ReportsMissingObservationDataInsteadOfGuessingNotVisible()
    {
        RunInRussian(localization =>
        {
            using var viewModel = CreateViewModel(
                localization,
                new ActiveMatchVisionParticipantSnapshot(
                    "enemy-player",
                    "Anivia",
                    "Anivia",
                    PlayerPosition.Middle,
                    TeamSide.Chaos,
                    MinimapMarkerAffiliation.Enemy,
                    VisibilityStatus: ParticipantVisibilityStatus.Unknown));

            viewModel.Refresh();

            Assert.Equal(
                "Нет данных о видимости",
                Assert.Single(viewModel.EnemyParticipantStatuses).StatusText);
        });
    }

    [Fact]
    public void Refresh_KeepsLastConfirmationTimeWhenObservationStreamIsLost()
    {
        RunInRussian(localization =>
        {
            var lastSeenAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
            using var viewModel = CreateViewModel(
                localization,
                new ActiveMatchVisionParticipantSnapshot(
                    "enemy-player",
                    "Anivia",
                    "Anivia",
                    PlayerPosition.Middle,
                    TeamSide.Chaos,
                    MinimapMarkerAffiliation.Enemy,
                    VisibilityStatus: ParticipantVisibilityStatus.Unknown,
                    LastSeenAt: lastSeenAt));

            viewModel.Refresh();

            Assert.Equal(
                string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    "Нет данных · последнее подтверждение в {0:HH:mm:ss}",
                    lastSeenAt.ToLocalTime()),
                Assert.Single(viewModel.EnemyParticipantStatuses).StatusText);
        });
    }

    [Fact]
    public void Refresh_ShowsGameTimeOfTheLastSighting()
    {
        RunInRussian(localization =>
        {
            using var viewModel = CreateViewModel(
                localization,
                new ActiveMatchVisionParticipantSnapshot(
                    "enemy-player",
                    "Anivia",
                    "Anivia",
                    PlayerPosition.Middle,
                    TeamSide.Chaos,
                    MinimapMarkerAffiliation.Enemy,
                    VisibilityStatus: ParticipantVisibilityStatus.NotVisible,
                    LastSeenAt: new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero),
                    LastSeenGameTime: TimeSpan.FromSeconds(754)));

            viewModel.Refresh();

            // Игровое время, а не часы: по нему игрок считает и возрождение, и
            // подход соперника, и на паузе одно из другого не пересчитать.
            Assert.Equal(
                "Не виден · последний раз в 12:34",
                Assert.Single(viewModel.EnemyParticipantStatuses).StatusText);
        });
    }

    [Fact]
    public void Refresh_FallsBackToTheClockBeforeTheFirstGameTime()
    {
        RunInRussian(localization =>
        {
            var lastSeenAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
            using var viewModel = CreateViewModel(
                localization,
                new ActiveMatchVisionParticipantSnapshot(
                    "enemy-player",
                    "Anivia",
                    "Anivia",
                    PlayerPosition.Middle,
                    TeamSide.Chaos,
                    MinimapMarkerAffiliation.Enemy,
                    VisibilityStatus: ParticipantVisibilityStatus.NotVisible,
                    LastSeenAt: lastSeenAt));

            viewModel.Refresh();

            // До первого ответа Live Client Data игрового времени ещё нет.
            Assert.Equal(
                string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    "Не виден · последний раз в {0:HH:mm:ss}",
                    lastSeenAt.ToLocalTime()),
                Assert.Single(viewModel.EnemyParticipantStatuses).StatusText);
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
                MinimapMarkerAffiliation.Ally,
                VisibilityStatus: ParticipantVisibilityStatus.NotVisible),
            new ActiveMatchVisionParticipantSnapshot(
                "enemy-player",
                "Anivia",
                "Anivia",
                PlayerPosition.Middle,
                TeamSide.Order,
                MinimapMarkerAffiliation.Enemy,
                VisibilityStatus: ParticipantVisibilityStatus.Visible,
                LastSeenAt: DateTimeOffset.UtcNow,
                VisibilityConfidence: 0.86)
        ]);

    private static CaptureDiagnosticsViewModel CreateViewModel(
        LocalizationService localization,
        ActiveMatchVisionParticipantSnapshot participant) =>
        new(
            new TestWindowCaptureSession(),
            isSupported: true,
            localization,
            new TestVisionDiagnosticsSource(new ActiveMatchVisionDiagnosticsSnapshot(
                TeamSide.Order,
                [participant])));

    private static void RunInRussian(Action<LocalizationService> action)
    {
        WpfTestHost.Run(() =>
        {
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;
            try
            {
                localization.SetLanguage("ru-RU");
                action(localization);
            }
            finally
            {
                localization.SetLanguage(initialLanguage);
            }
        });
    }

    private sealed class TestVisionDiagnosticsSource(
        ActiveMatchVisionDiagnosticsSnapshot snapshot) : IActiveMatchVisionDiagnosticsSource
    {
        public ActiveMatchVisionDiagnosticsSnapshot DiagnosticsSnapshot { get; } = snapshot;
    }
}
