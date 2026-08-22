namespace TempoCue.Overlay.Tests;

public sealed class OverlayNotificationSchedulerTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 11, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Publish_OrdersTwoDifferentPrioritiesAndReplacesLowestWhenSlotsAreFull()
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
    public void Publish_StacksEqualPriorityCardsInArrivalOrderWithIndependentLifetime()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(
            Create("first-coach", OverlayNotificationTone.Coach),
            Start);

        var second = scheduler.Publish(
            Create("second-coach", OverlayNotificationTone.Coach),
            Start.AddSeconds(1));

        Assert.Equal(
            ["first-coach", "second-coach"],
            second.Items.Select(item => item.Notification.Key));
        Assert.Equal(Start.AddSeconds(7), second.Items[0].ExpiresAt);
        Assert.Equal(Start.AddSeconds(8), second.Items[1].ExpiresAt);

        var afterFirstExpiration = scheduler.Advance(Start.AddSeconds(7));

        Assert.Equal(
            "second-coach",
            Assert.Single(afterFirstExpiration.Items).Notification.Key);
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
    public void Publish_CoachUsesSecondarySlotBelowVisibleWarning()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("caution", OverlayNotificationTone.Caution), Start);
        var snapshot = scheduler.Publish(
            Create("coach", OverlayNotificationTone.Coach),
            Start.AddSeconds(1));

        Assert.Equal(
            ["caution", "coach"],
            snapshot.Items.Select(item => item.Notification.Key));
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
    public void Advance_PromotesLearningCardAfterCadenceWithoutVisibleCards()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create(
            "coach",
            OverlayNotificationTone.Coach,
            purpose: OverlayNotificationPurpose.Learning), Start);
        scheduler.Publish(Create(
            "info",
            OverlayNotificationTone.Information,
            purpose: OverlayNotificationPurpose.Learning), Start.AddSeconds(1));

        var betweenCards = scheduler.Advance(Start.AddSeconds(7));
        Assert.Empty(betweenCards.Items);
        Assert.True(betweenCards.HasPending);

        var promoted = scheduler.Advance(Start.AddSeconds(8));
        Assert.Equal("info", Assert.Single(promoted.Items).Notification.Key);
        Assert.False(promoted.HasPending);
    }

    [Fact]
    public void Publish_OperationalNotificationBypassesLearningCadence()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create(
            "learning",
            OverlayNotificationTone.Coach,
            purpose: OverlayNotificationPurpose.Learning), Start);

        var snapshot = scheduler.Publish(
            Create("objective", OverlayNotificationTone.Information),
            Start.AddSeconds(1));

        Assert.Equal(
            ["objective", "learning"],
            snapshot.Items.Select(item => item.Notification.Key));
    }

    [Fact]
    public void Publish_LearningNotificationCannotDisplaceOperationalCards()
    {
        var scheduler = new OverlayNotificationScheduler();
        scheduler.Publish(Create("no-data", OverlayNotificationTone.NoData), Start);
        scheduler.Publish(Create("information", OverlayNotificationTone.Information), Start);

        var snapshot = scheduler.Publish(
            Create(
                "learning-danger",
                OverlayNotificationTone.Danger,
                purpose: OverlayNotificationPurpose.Learning),
            Start.AddSeconds(1));

        Assert.Equal(
            ["information", "no-data"],
            snapshot.Items.Select(item => item.Notification.Key));
        Assert.True(snapshot.HasPending);
    }

    [Fact]
    public void Publish_OperationalNotificationDisplacesLearningCardRegardlessOfTone()
    {
        var scheduler = new OverlayNotificationScheduler(new OverlayDisplayPolicy
        {
            LearningCadence = TimeSpan.Zero
        });
        scheduler.Publish(Create(
            "learning-danger",
            OverlayNotificationTone.Danger,
            purpose: OverlayNotificationPurpose.Learning), Start);
        scheduler.Publish(Create(
            "learning-caution",
            OverlayNotificationTone.Caution,
            purpose: OverlayNotificationPurpose.Learning), Start);

        var snapshot = scheduler.Publish(
            Create("operational-no-data", OverlayNotificationTone.NoData),
            Start.AddSeconds(1));

        Assert.Equal(
            ["operational-no-data", "learning-danger"],
            snapshot.Items.Select(item => item.Notification.Key));
    }

    [Fact]
    public void Publish_ShowsRepeatedMapAwarenessEventOnlyOncePerSuppressionInterval()
    {
        // Появление вражеского лесника использует стабильный ключ участника,
        // поэтому повторное событие не должно превращаться во вторую карточку,
        // пока не истечёт окно подавления.
        const string key = "map-awareness:jungle:enemy-jungle:appeared";
        var scheduler = new OverlayNotificationScheduler();
        var suppression = new OverlayDisplayPolicy().DuplicateSuppressionInterval;

        scheduler.Publish(Create(key, OverlayNotificationTone.Danger), Start);
        Assert.Single(scheduler.Current.Items);

        // Повтор, пока карточка ещё на экране: обновляет её, а не добавляет вторую.
        scheduler.Publish(
            Create(key, OverlayNotificationTone.Danger),
            Start.AddSeconds(2));
        Assert.Single(scheduler.Current.Items);

        // Карточка уже исчезла по времени жизни, но окно подавления не истекло:
        // повторное появление того же лесника не показывается вовсе.
        var beforeWindowEnds = Start + suppression - TimeSpan.FromSeconds(1);
        scheduler.Publish(Create(key, OverlayNotificationTone.Danger), beforeWindowEnds);
        Assert.Empty(scheduler.Current.Items);

        // После окна подавления настоящее повторное появление снова показывается.
        var afterWindowEnds = Start + suppression + TimeSpan.FromSeconds(1);
        scheduler.Publish(Create(key, OverlayNotificationTone.Danger), afterWindowEnds);
        Assert.Equal(
            [key],
            scheduler.Current.Items.Select(item => item.Notification.Key));
    }

    private static OverlayNotification Create(
        string key,
        OverlayNotificationTone tone,
        string phrase = "Фраза",
        OverlayNotificationPurpose purpose = OverlayNotificationPurpose.Operational) => new(
        key,
        "Статус",
        phrase,
        "Источник",
        "Закрыть",
        tone,
        purpose: purpose);
}
