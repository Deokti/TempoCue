using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Overlay;
using static TempoCue.App.Tests.ActiveMatchOverlayTestData;

namespace TempoCue.App.Tests;

/// <summary>Проверяет границу между готовым советом активного матча и оверлеем.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class ActiveMatchOverlayCoordinatorTests
{
    [Fact]
    public void Updated_ShowsEachMatchupPhaseOnlyOnce()
    {
        LocalizationService.Instance.SetLanguage("ru-RU");
        var monitor = new TestActiveMatchMonitor();
        using var presenter = new RecordingOverlayPresenter();
        using var coordinator = new ActiveMatchOverlayCoordinator(
            monitor,
            presenter,
            LocalizationService.Instance);
        var pack = TestCoachPackFactory.CreateMatchup();
        var earlyAdvice = CreateAdvice("early", PowerState.Dangerous, "Не начинай размен");

        monitor.Publish(CreateSuccessUpdate(pack, earlyAdvice, gameTimeSeconds: 30, level: 1));
        monitor.Publish(CreateSuccessUpdate(pack, earlyAdvice, gameTimeSeconds: 31, level: 1));
        monitor.Publish(CreateSuccessUpdate(
            pack,
            CreateAdvice("mid", PowerState.Cautious, "Играй короткими разменами"),
            gameTimeSeconds: 540,
            level: 9));

        Assert.Collection(
            presenter.Notifications,
            first =>
            {
                Assert.Equal("matchup:test.urgot-vs-sett:early", first.Key);
                Assert.Equal("Не начинай размен", first.Phrase);
                Assert.Equal("Содержимое совета", first.Source);
                Assert.Equal("Уровень 1", first.Badge);
                Assert.Equal(OverlayNotificationTone.Danger, first.Tone);
            },
            second =>
            {
                Assert.Equal("matchup:test.urgot-vs-sett:mid", second.Key);
                Assert.Equal("Уровень 9", second.Badge);
                Assert.Equal(OverlayNotificationTone.Caution, second.Tone);
            });
        Assert.Equal(OverlayInteractionMode.Game, presenter.InteractionMode);
    }

    [Fact]
    public void Updated_AllowsSamePhaseAgainAfterMatchReset()
    {
        var monitor = new TestActiveMatchMonitor();
        using var presenter = new RecordingOverlayPresenter();
        using var coordinator = new ActiveMatchOverlayCoordinator(
            monitor,
            presenter,
            LocalizationService.Instance);
        var pack = TestCoachPackFactory.CreateMatchup();
        var advice = CreateAdvice("early", PowerState.Playable, "Контролируй волну");

        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 600, level: 10));
        monitor.Publish(new ActiveMatchUpdate(
            MatchSnapshotReadStatus.NoActiveMatch,
            null,
            null));
        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 20, level: 1));

        Assert.Equal(2, presenter.Notifications.Count);
    }

    [Fact]
    public void Updated_DoesNotResetPhaseAfterTransientReadError()
    {
        var monitor = new TestActiveMatchMonitor();
        using var presenter = new RecordingOverlayPresenter();
        using var coordinator = new ActiveMatchOverlayCoordinator(
            monitor,
            presenter,
            LocalizationService.Instance);
        var pack = TestCoachPackFactory.CreateMatchup();
        var advice = CreateAdvice("early", PowerState.Favorable, "Дави линию");

        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 60, level: 2));
        monitor.Publish(new ActiveMatchUpdate(MatchSnapshotReadStatus.Timeout, null, null));
        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 62, level: 2));

        Assert.Single(presenter.Notifications);
    }

    [Fact]
    public void Updated_ShowsOnePersistentTipOnNextLevelWithoutPhaseChange()
    {
        LocalizationService.Instance.SetLanguage("ru-RU");
        var monitor = new TestActiveMatchMonitor();
        using var presenter = new RecordingOverlayPresenter();
        using var coordinator = new ActiveMatchOverlayCoordinator(
            monitor,
            presenter,
            LocalizationService.Instance);
        var pack = TestCoachPackFactory.CreateMatchup(
            persistentTips:
            [
                CreatePersistentTip("phase-tip", "Совет текущей фазы", "early"),
                CreatePersistentTip("general-tip", "Совет на весь матч")
            ]);
        var advice = CreateAdvice("early", PowerState.Dangerous, "Фаза");

        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 30, level: 1));
        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 60, level: 2));
        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 61, level: 2));
        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 90, level: 3));

        Assert.Collection(
            presenter.Notifications,
            phase => Assert.Equal("matchup:test.urgot-vs-sett:early", phase.Key),
            phaseTip =>
            {
                Assert.Equal("matchup:test.urgot-vs-sett:tip:phase-tip:level:2", phaseTip.Key);
                Assert.Equal("ПОДСКАЗКА ПО МАТЧАПУ", phaseTip.DisplayStatus);
                Assert.Equal("Совет текущей фазы", phaseTip.Phrase);
                Assert.Equal("Уровень 2", phaseTip.Badge);
                Assert.Equal(OverlayNotificationTone.Coach, phaseTip.Tone);
            },
            generalTip =>
                Assert.Equal("matchup:test.urgot-vs-sett:tip:general-tip:level:3", generalTip.Key));
    }

    [Fact]
    public void Updated_PrioritizesPhaseChangeOverPersistentTip()
    {
        var monitor = new TestActiveMatchMonitor();
        using var presenter = new RecordingOverlayPresenter();
        using var coordinator = new ActiveMatchOverlayCoordinator(
            monitor,
            presenter,
            LocalizationService.Instance);
        var pack = TestCoachPackFactory.CreateMatchup(
            rules:
            [
                TestCoachPackFactory.CreateRule(
                    "early",
                    1,
                    1,
                    PowerState.Dangerous,
                    "Ранняя фаза"),
                TestCoachPackFactory.CreateRule(
                    "late",
                    2,
                    null,
                    PowerState.Playable,
                    "Следующая фаза")
            ],
            persistentTips: [CreatePersistentTip("general-tip", "Общий совет")]);

        monitor.Publish(CreateSuccessUpdate(
            pack,
            CreateAdvice("early", PowerState.Dangerous, "Ранняя фаза"),
            gameTimeSeconds: 30,
            level: 1));
        monitor.Publish(CreateSuccessUpdate(
            pack,
            CreateAdvice("late", PowerState.Playable, "Следующая фаза"),
            gameTimeSeconds: 60,
            level: 2));
        monitor.Publish(CreateSuccessUpdate(
            pack,
            CreateAdvice("late", PowerState.Playable, "Следующая фаза"),
            gameTimeSeconds: 90,
            level: 3));

        Assert.Collection(
            presenter.Notifications,
            first => Assert.EndsWith(":early", first.Key),
            second => Assert.EndsWith(":late", second.Key),
            third => Assert.EndsWith(":tip:general-tip:level:3", third.Key));
    }

    [Fact]
    public void Updated_RepeatsSinglePersistentTipOnlyAfterThreeLevelGap()
    {
        var monitor = new TestActiveMatchMonitor();
        using var presenter = new RecordingOverlayPresenter();
        using var coordinator = new ActiveMatchOverlayCoordinator(
            monitor,
            presenter,
            LocalizationService.Instance);
        var pack = TestCoachPackFactory.CreateMatchup(
            persistentTips: [CreatePersistentTip("only-tip", "Единственный совет")]);
        var advice = CreateAdvice("early", PowerState.Dangerous, "Фаза");

        for (var level = 1; level <= 5; level++)
        {
            monitor.Publish(CreateSuccessUpdate(
                pack,
                advice,
                gameTimeSeconds: level * 30,
                level));
        }

        Assert.Equal(3, presenter.Notifications.Count);
        Assert.Equal("Уровень 2", presenter.Notifications[1].Badge);
        Assert.Equal("Уровень 5", presenter.Notifications[2].Badge);
        Assert.NotEqual(
            presenter.Notifications[1].Key,
            presenter.Notifications[2].Key);
    }

    [Fact]
    public void Updated_AllowsPersistentTipsAgainAfterMatchReset()
    {
        var monitor = new TestActiveMatchMonitor();
        using var presenter = new RecordingOverlayPresenter();
        using var coordinator = new ActiveMatchOverlayCoordinator(
            monitor,
            presenter,
            LocalizationService.Instance);
        var pack = TestCoachPackFactory.CreateMatchup(
            persistentTips: [CreatePersistentTip("general-tip", "Общий совет")]);
        var advice = CreateAdvice("early", PowerState.Playable, "Фаза");

        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 30, level: 1));
        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 60, level: 2));
        monitor.Publish(new ActiveMatchUpdate(
            MatchSnapshotReadStatus.NoActiveMatch,
            null,
            null));
        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 30, level: 1));
        monitor.Publish(CreateSuccessUpdate(pack, advice, gameTimeSeconds: 60, level: 2));

        Assert.Equal(
            2,
            presenter.Notifications.Count(notification =>
                notification.Key.Contains(":tip:general-tip:level:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Dispose_UnsubscribesFromMatchUpdates()
    {
        var monitor = new TestActiveMatchMonitor();
        using var presenter = new RecordingOverlayPresenter();
        var coordinator = new ActiveMatchOverlayCoordinator(
            monitor,
            presenter,
            LocalizationService.Instance);
        coordinator.Dispose();

        monitor.Publish(CreateSuccessUpdate(
            TestCoachPackFactory.CreateMatchup(),
            CreateAdvice("early", PowerState.Dangerous, "Совет"),
            gameTimeSeconds: 10,
            level: 1));

        Assert.Empty(presenter.Notifications);
    }

}
