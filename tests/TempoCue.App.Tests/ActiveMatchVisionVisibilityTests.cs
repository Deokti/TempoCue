using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Проверяет путь от наблюдения Vision до устойчивого статуса участника.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class ActiveMatchVisionVisibilityTests
{
    [Fact]
    public async Task ConsecutiveObservations_ConfirmParticipantAndPreservePosition()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var observations = new ActiveMatchVisionTestSupport.TestObservationSource();
        using var coordinator = CreateCoordinator(monitor, sink, observations);
        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
        await sink.WaitForReferencesAsync(2);
        var capturedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

        observations.Publish(capturedAt, CreateEnemyAniviaCandidate());

        var confirming = FindEnemy(coordinator);
        Assert.Equal(ParticipantVisibilityStatus.Confirming, confirming.VisibilityStatus);

        observations.Publish(
            capturedAt.AddMilliseconds(250),
            CreateEnemyAniviaCandidate());

        var visible = FindEnemy(coordinator);
        Assert.Equal(ParticipantVisibilityStatus.Visible, visible.VisibilityStatus);
        // Видимость решается сходством с портретом, а не итоговой оценкой:
        // шумный контур не должен топить хорошее совпадение (TC-008).
        Assert.Equal(0.86, visible.VisibilityConfidence, 2);
        Assert.Equal(0.34375, Assert.IsType<double>(visible.MapX), 5);
        Assert.Equal(0.4375, Assert.IsType<double>(visible.MapY), 5);
        Assert.Equal(capturedAt.AddMilliseconds(250), visible.LastSeenAt);
    }

    [Fact]
    public async Task DeadParticipant_IsRemovedFromVisibilityTracking()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var observations = new ActiveMatchVisionTestSupport.TestObservationSource();
        using var coordinator = CreateCoordinator(monitor, sink, observations);
        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
        await sink.WaitForReferencesAsync(2);
        var capturedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        observations.Publish(capturedAt, CreateEnemyAniviaCandidate());
        observations.Publish(
            capturedAt.AddMilliseconds(250),
            CreateEnemyAniviaCandidate());
        Assert.Equal(
            ParticipantVisibilityStatus.Visible,
            FindEnemy(coordinator).VisibilityStatus);

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate(enemyIsDead: true));
        observations.Publish(
            capturedAt.AddMilliseconds(500),
            CreateEnemyAniviaCandidate());

        var dead = FindEnemy(coordinator);
        Assert.True(dead.IsDead);
        Assert.Equal(ParticipantVisibilityStatus.Unknown, dead.VisibilityStatus);
        Assert.Null(dead.LastSeenAt);
    }

    [Fact]
    public async Task EmptyObservation_ExpiresVisibilityAfterGracePeriod()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var observations = new ActiveMatchVisionTestSupport.TestObservationSource();
        using var coordinator = CreateCoordinator(monitor, sink, observations);
        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
        await sink.WaitForReferencesAsync(2);
        var capturedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        observations.Publish(capturedAt, CreateEnemyAniviaCandidate());
        observations.Publish(
            capturedAt.AddMilliseconds(250),
            CreateEnemyAniviaCandidate());
        Assert.Equal(
            ParticipantVisibilityStatus.Visible,
            FindEnemy(coordinator).VisibilityStatus);

        // Кадр без кандидатов приходит и при потерянной миникарте: он должен
        // завершить удержание видимости точно так же, как обычный пропуск.
        observations.Publish(capturedAt.AddMilliseconds(1500));

        var expired = FindEnemy(coordinator);
        Assert.Equal(ParticipantVisibilityStatus.NotVisible, expired.VisibilityStatus);
        Assert.Equal(capturedAt.AddMilliseconds(250), expired.LastSeenAt);
    }

    [Fact]
    public async Task FailedObservation_AgesVisibilityWithoutConfirmingStream()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var observations = new ActiveMatchVisionTestSupport.TestObservationSource();
        using var coordinator = CreateCoordinator(
            monitor,
            sink,
            observations,
            observationTimeout: TimeSpan.Zero);
        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
        await sink.WaitForReferencesAsync(2);
        var capturedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        observations.Publish(capturedAt, CreateEnemyAniviaCandidate());
        observations.Publish(
            capturedAt.AddMilliseconds(250),
            CreateEnemyAniviaCandidate());
        Assert.Equal(
            ParticipantVisibilityStatus.Visible,
            FindEnemy(coordinator).VisibilityStatus);

        // Продолжительная ошибка чтения не подтверждает поток: статус честно
        // становится «нет данных», а не «не виден».
        observations.PublishFailed(capturedAt.AddMilliseconds(1500));

        var failed = FindEnemy(coordinator);
        Assert.Equal(ParticipantVisibilityStatus.Unknown, failed.VisibilityStatus);
        Assert.Equal(capturedAt.AddMilliseconds(250), failed.LastSeenAt);
    }

    [Fact]
    public async Task FailedObservation_PreservesVisibilityWhileStreamIsRecentlyAlive()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var observations = new ActiveMatchVisionTestSupport.TestObservationSource();
        using var coordinator = CreateCoordinator(monitor, sink, observations);
        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
        await sink.WaitForReferencesAsync(2);
        var capturedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        observations.Publish(capturedAt, CreateEnemyAniviaCandidate());
        observations.Publish(
            capturedAt.AddMilliseconds(250),
            CreateEnemyAniviaCandidate());

        // Непрочитанный кадр — не доказательство отсутствия: пока поток недавно
        // жив, подтверждённая видимость сохраняется, а не превращается в «не виден».
        observations.PublishFailed(capturedAt.AddMilliseconds(1500));

        var preserved = FindEnemy(coordinator);
        Assert.Equal(ParticipantVisibilityStatus.Visible, preserved.VisibilityStatus);
        Assert.Equal(capturedAt.AddMilliseconds(250), preserved.LastSeenAt);
    }

    [Fact]
    public async Task StaleObservationStream_DegradesVisibilityToUnknown()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var observations = new ActiveMatchVisionTestSupport.TestObservationSource();
        using var coordinator = CreateCoordinator(
            monitor,
            sink,
            observations,
            observationTimeout: TimeSpan.Zero);
        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
        await sink.WaitForReferencesAsync(2);
        var capturedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        observations.Publish(capturedAt, CreateEnemyAniviaCandidate());
        observations.Publish(
            capturedAt.AddMilliseconds(250),
            CreateEnemyAniviaCandidate());
        Assert.Equal(
            ParticipantVisibilityStatus.Visible,
            FindEnemy(coordinator).VisibilityStatus);

        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());

        var stale = FindEnemy(coordinator);
        Assert.Equal(ParticipantVisibilityStatus.Unknown, stale.VisibilityStatus);
        Assert.Equal(capturedAt.AddMilliseconds(250), stale.LastSeenAt);
    }

    private static ActiveMatchVisionCoordinator CreateCoordinator(
        TestActiveMatchMonitor monitor,
        IMinimapChampionReferenceSink sink,
        IMinimapChampionObservationSource observations,
        TimeSpan? observationTimeout = null) =>
        new(
            monitor,
            ActiveMatchVisionTestSupport.CreateCatalog(),
            new ActiveMatchVisionTestSupport.TestPortraitReferenceLoader(),
            sink,
            LocalizationService.Instance,
            observations,
            observationTimeout);

    private static ActiveMatchVisionParticipantSnapshot FindEnemy(
        ActiveMatchVisionCoordinator coordinator) =>
        Assert.Single(
            coordinator.DiagnosticsSnapshot.Participants,
            participant => participant.ParticipantId == "enemy-player");

    private static MinimapChampionMarkerCandidate CreateEnemyAniviaCandidate() =>
        new(
            X: 100,
            Y: 120,
            Width: 20,
            Height: 40,
            Affiliation: MinimapMarkerAffiliation.Enemy,
            Confidence: 0.90,
            ChampionId: "Anivia",
            IdentityConfidence: 0.86);
}
