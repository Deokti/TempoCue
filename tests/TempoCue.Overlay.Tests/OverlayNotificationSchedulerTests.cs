namespace TempoCue.Overlay.Tests;

public sealed class OverlayNotificationSchedulerTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 11, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Publish_OrdersTwoDifferentPrioritiesAndReplacesEqualPriority()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("caution", OverlayNotificationTone.Caution), Start);
        scheduler.Publish(Create("info", OverlayNotificationTone.Information), Start);

        Assert.Equal(
            ["caution", "info"],
            scheduler.Current.Items.Select(item => item.Notification.Key));

        scheduler.Publish(Create("new-info", OverlayNotificationTone.Information), Start);

        Assert.Equal(
            ["caution", "new-info"],
            scheduler.Current.Items.Select(item => item.Notification.Key));
    }

    [Fact]
    public void Publish_DangerImmediatelyClearsLowerPriorityCards()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("caution", OverlayNotificationTone.Caution), Start);
        scheduler.Publish(Create("info", OverlayNotificationTone.Information), Start);

        scheduler.Publish(Create("danger", OverlayNotificationTone.Danger), Start.AddSeconds(1));

        var item = Assert.Single(scheduler.Current.Items);
        Assert.Equal("danger", item.Notification.Key);
        Assert.Equal(Start.AddSeconds(9), item.ExpiresAt);
    }

    [Fact]
    public void Publish_CoachWaitsUntilWarningExpires()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("caution", OverlayNotificationTone.Caution), Start);
        scheduler.Publish(Create("coach", OverlayNotificationTone.Coach), Start.AddSeconds(1));

        Assert.Equal("caution", Assert.Single(scheduler.Current.Items).Notification.Key);

        var afterWarning = scheduler.Advance(Start.AddSeconds(6));

        Assert.Equal("coach", Assert.Single(afterWarning.Items).Notification.Key);
    }

    [Fact]
    public void Publish_UpdatesSameKeyInPlaceWithoutChangingSequence()
    {
        var scheduler = new OverlayNotificationScheduler();
        var first = scheduler.Publish(Create("event", OverlayNotificationTone.Information), Start);
        var sequence = Assert.Single(first.Items).Sequence;

        var updated = scheduler.Publish(
            Create("event", OverlayNotificationTone.Information, "Обновлённая фраза"),
            Start.AddSeconds(1));

        var item = Assert.Single(updated.Items);
        Assert.Equal(sequence, item.Sequence);
        Assert.Equal("Обновлённая фраза", item.Notification.Phrase);
        Assert.Equal(Start.AddSeconds(6), item.ExpiresAt);
    }

    [Fact]
    public void Publish_ExtendsLifetimeForLongAdviceText()
    {
        var scheduler = new OverlayNotificationScheduler();
        var longAdvice = new string('a', 180);

        var snapshot = scheduler.Publish(
            Create("long-coach", OverlayNotificationTone.Coach, longAdvice),
            Start);

        var item = Assert.Single(snapshot.Items);
        Assert.Equal(Start.AddSeconds(13), item.ExpiresAt);
    }

    [Fact]
    public void Publish_SuppressesRecentlyCompletedDuplicate()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("event", OverlayNotificationTone.Information), Start);
        Assert.Empty(scheduler.Advance(Start.AddSeconds(5)).Items);

        scheduler.Publish(Create("event", OverlayNotificationTone.Information), Start.AddSeconds(10));
        Assert.Empty(scheduler.Current.Items);

        scheduler.Publish(Create("event", OverlayNotificationTone.Information), Start.AddSeconds(20));
        Assert.Equal("event", Assert.Single(scheduler.Current.Items).Notification.Key);
    }

    [Fact]
    public void Reset_AllowsImmediateManualPreviewWithSameKey()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("settings.preview", OverlayNotificationTone.Information), Start);
        scheduler.Reset();

        scheduler.Publish(
            Create("settings.preview", OverlayNotificationTone.Information),
            Start.AddSeconds(1));

        Assert.Equal(
            "settings.preview",
            Assert.Single(scheduler.Current.Items).Notification.Key);
    }

    [Fact]
    public void Publish_PausedIsPersistentAndBlocksOtherCards()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("paused", OverlayNotificationTone.Paused), Start);
        scheduler.Publish(Create("danger", OverlayNotificationTone.Danger), Start.AddMinutes(1));

        var item = Assert.Single(scheduler.Advance(Start.AddHours(1)).Items);
        Assert.Equal("paused", item.Notification.Key);
        Assert.Null(item.ExpiresAt);
    }

    [Fact]
    public void Advance_PromotesLowPriorityCardAfterCadenceWithoutVisibleCards()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("coach", OverlayNotificationTone.Coach), Start);
        scheduler.Publish(Create("info", OverlayNotificationTone.Information), Start.AddSeconds(1));

        var betweenCards = scheduler.Advance(Start.AddSeconds(7));
        Assert.Empty(betweenCards.Items);
        Assert.True(betweenCards.HasPending);

        var promoted = scheduler.Advance(Start.AddSeconds(8));
        Assert.Equal("info", Assert.Single(promoted.Items).Notification.Key);
        Assert.False(promoted.HasPending);
    }

    private static OverlayNotification Create(
        string key,
        OverlayNotificationTone tone,
        string phrase = "Фраза") => new(
        key,
        "Статус",
        phrase,
        "Источник",
        "Закрыть",
        tone);
}
