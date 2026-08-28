using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.Application;
using TempoCue.Coaching;
using TempoCue.Core;
using TempoCue.Overlay;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class ActiveMatchMapAwarenessCoordinatorTests
{
    [Theory]
    [InlineData(
        "enemy-jungle",
        ParticipantVisibilityTransitionKind.Appeared,
        "map-awareness:jungle-appeared:enemy-jungle",
        OverlayNotificationTone.Danger,
        "Amumu появился на карте")]
    [InlineData(
        "enemy-mid",
        ParticipantVisibilityTransitionKind.Disappeared,
        "map-awareness:mid-disappeared:enemy-mid",
        OverlayNotificationTone.Caution,
        "Anivia пропал из вида")]
    public void TransitionAvailable_ShowsNotificationFromTheMatchedRule(
        string participantId,
        ParticipantVisibilityTransitionKind kind,
        string expectedKey,
        OverlayNotificationTone expectedTone,
        string expectedPhrase)
    {
        LocalizationService.Instance.SetLanguage("ru-RU");
        var monitor = new TestActiveMatchMonitor();
        var source = new TestParticipantVisibilityTransitionSource();
        using var presenter = new RecordingOverlayPresenter();
        var settings = new OverlaySettingsState(new OverlayUserSettings
        {
            BackgroundOpacityPercent = 70
        });
        using var coordinator = new ActiveMatchMapAwarenessCoordinator(
            monitor,
            source,
            presenter,
            LocalizationService.Instance,
            settings);
        monitor.Publish(CreateMatchUpdate());

        source.Publish(new ParticipantVisibilityTransition(
            participantId,
            kind,
            DateTimeOffset.UtcNow,
            null,
            0.95,
            0.5,
            0.5));

        var notification = Assert.Single(presenter.Notifications);
        Assert.Equal(expectedKey, notification.Key);
        Assert.Equal(expectedTone, notification.Tone);
        // Главная фраза называет событие, а не только чемпиона: одного имени
        // мало, чтобы понять, что случилось.
        Assert.Equal(expectedPhrase, notification.Phrase);
        Assert.Equal(OverlayNotificationPurpose.Operational, notification.Purpose);
        Assert.Equal(OverlayInteractionMode.Game, presenter.InteractionMode);
        Assert.Equal(0.7, presenter.Settings.BackgroundOpacity);
    }

    [Fact]
    public void Dispose_UnsubscribesFromTransitionSource()
    {
        var monitor = new TestActiveMatchMonitor();
        var source = new TestParticipantVisibilityTransitionSource();
        using var presenter = new RecordingOverlayPresenter();
        var coordinator = new ActiveMatchMapAwarenessCoordinator(
            monitor,
            source,
            presenter,
            LocalizationService.Instance,
            new OverlaySettingsState(new OverlayUserSettings()));
        monitor.Publish(CreateMatchUpdate());

        coordinator.Dispose();
        source.Publish(new ParticipantVisibilityTransition(
            "enemy-jungle",
            ParticipantVisibilityTransitionKind.Appeared,
            DateTimeOffset.UtcNow,
            null,
            0.95,
            0.5,
            0.5));

        Assert.Empty(presenter.Notifications);
    }

    private static ActiveMatchUpdate CreateMatchUpdate()
    {
        var match = new MatchState
        {
            GameTimeSeconds = 180,
            ActivePlayerId = "active",
            Players =
            [
                CreatePlayer("active", "Urgot", TeamSide.Order, PlayerPosition.Top),
                CreatePlayer("enemy-jungle", "Amumu", TeamSide.Chaos, PlayerPosition.Jungle),
                CreatePlayer("enemy-mid", "Anivia", TeamSide.Chaos, PlayerPosition.Middle)
            ]
        };

        // Правила приходят из включённых наборов, а не из кода координатора.
        var plan = new ActiveMapControlPlan(
            [],
            ["Проверочный набор"],
            [],
            [
                new MapVisionRule(
                    "jungle-appeared",
                    PlayerPosition.Jungle,
                    MapVisionTransition.Appeared,
                    MapObjectiveReminderPriority.Important,
                    "Лесник виден на карте.",
                    "Положение лесника быстро устаревает.",
                    "Реши, даёт ли это окно."),
                new MapVisionRule(
                    "mid-disappeared",
                    PlayerPosition.Middle,
                    MapVisionTransition.Disappeared,
                    MapObjectiveReminderPriority.Caution,
                    "Мидер пропал с карты.",
                    "Мидер доходит до боковой линии быстрее всех.",
                    "Отойди к башне или поставь обзор.")
            ]);

        return new ActiveMatchUpdate(
            MatchSnapshotReadStatus.Success,
            new MatchSnapshot(match, [], DateTimeOffset.UtcNow),
            ActiveCoachPackSelection.NotFound(),
            plan);
    }

    private static PlayerState CreatePlayer(
        string participantId,
        string championName,
        TeamSide team,
        PlayerPosition position) => new()
    {
        ParticipantId = participantId,
        ChampionName = championName,
        Team = team,
        Position = position,
        Level = 6
    };

    private sealed class TestParticipantVisibilityTransitionSource
        : IParticipantVisibilityTransitionSource
    {
        public event EventHandler<ParticipantVisibilityTransition>? TransitionAvailable;

        public void Publish(ParticipantVisibilityTransition transition) =>
            TransitionAvailable?.Invoke(this, transition);
    }
}
