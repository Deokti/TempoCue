using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Проверяет публикацию устойчивых переходов за границей Vision.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class ActiveMatchVisionTransitionTests
{
    [Fact]
    public async Task ConfirmedAppearanceAndExpiredVisibility_ArePublishedOnce()
    {
        var monitor = new TestActiveMatchMonitor();
        var sink = new ActiveMatchVisionTestSupport.RecordingChampionReferenceSink();
        var observations = new ActiveMatchVisionTestSupport.TestObservationSource();
        using var coordinator = new ActiveMatchVisionCoordinator(
            monitor,
            ActiveMatchVisionTestSupport.CreateCatalog(),
            new ActiveMatchVisionTestSupport.TestPortraitReferenceLoader(),
            sink,
            LocalizationService.Instance,
            observations);
        var transitions = new List<ParticipantVisibilityTransition>();
        coordinator.TransitionAvailable += (_, transition) => transitions.Add(transition);
        monitor.Publish(ActiveMatchVisionTestSupport.CreateUpdate());
        await sink.WaitForReferencesAsync(2);
        var capturedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

        observations.Publish(capturedAt, CreateEnemyCandidate());
        Assert.Empty(transitions);

        observations.Publish(capturedAt.AddMilliseconds(250), CreateEnemyCandidate());
        var appeared = Assert.Single(transitions);
        Assert.Equal(ParticipantVisibilityTransitionKind.Appeared, appeared.Kind);
        Assert.Equal("enemy-player", appeared.ParticipantId);
        // В переход попадает сходство с портретом, а не итоговая оценка (TC-008).
        Assert.Equal(0.86, appeared.Confidence);

        // Ошибка чтения кадра — не доказательство отсутствия и не рождает переход.
        observations.PublishFailed(capturedAt.AddMilliseconds(1300));
        Assert.Single(transitions);

        // Исчезновение требует отрицательного подтверждения: нескольких успешных
        // кадров без участника и минимальной длительности отсутствия.
        observations.Publish(capturedAt.AddMilliseconds(1500));
        observations.Publish(capturedAt.AddMilliseconds(1750));
        observations.Publish(capturedAt.AddMilliseconds(2000));
        Assert.Single(transitions);

        observations.Publish(capturedAt.AddMilliseconds(2250));
        Assert.Equal(2, transitions.Count);
        Assert.Equal(
            ParticipantVisibilityTransitionKind.Disappeared,
            transitions[1].Kind);

        // Продолжение отсутствия не повторяет событие.
        observations.Publish(capturedAt.AddMilliseconds(2500));
        Assert.Equal(2, transitions.Count);
    }

    private static MinimapChampionMarkerCandidate CreateEnemyCandidate() =>
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
