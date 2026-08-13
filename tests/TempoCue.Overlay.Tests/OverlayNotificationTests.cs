namespace TempoCue.Overlay.Tests;

public sealed class OverlayNotificationTests
{
    [Fact]
    public void Constructor_TrimsTextAndKeepsPresentationData()
    {
        var notification = new OverlayNotification(
            "  matchup.rule.1  ",
            "  Совет  ",
            "  Не вступайте в бой  ",
            "  Набор советов · Urgot против Sett  ",
            "  Закрыть оверлей  ",
            OverlayNotificationTone.Coach,
            "  уровни 1–8  ",
            "  новое  ",
            OverlayNotificationPurpose.Learning);

        Assert.Equal("matchup.rule.1", notification.Key);
        Assert.Equal("Совет", notification.Status);
        Assert.Equal("Не вступайте в бой", notification.Phrase);
        Assert.Equal("Набор советов · Urgot против Sett", notification.Source);
        Assert.Equal("Закрыть оверлей", notification.DismissLabel);
        Assert.Equal(OverlayNotificationTone.Coach, notification.Tone);
        Assert.Equal(OverlayNotificationPurpose.Learning, notification.Purpose);
        Assert.Equal("уровни 1–8", notification.Context);
        Assert.Equal("новое", notification.Badge);
    }

    [Fact]
    public void Constructor_DefaultsPurposeToOperational()
    {
        var notification = new OverlayNotification(
            "objective.dragon",
            "Событие",
            "Дракон появится через минуту",
            "Контроль карты",
            "Закрыть");

        Assert.Equal(OverlayNotificationPurpose.Operational, notification.Purpose);
    }

    [Theory]
    [InlineData("", "Статус", "Фраза", "Источник", "Закрыть")]
    [InlineData("key", " ", "Фраза", "Источник", "Закрыть")]
    [InlineData("key", "Статус", "\t", "Источник", "Закрыть")]
    [InlineData("key", "Статус", "Фраза", "", "Закрыть")]
    [InlineData("key", "Статус", "Фраза", "Источник", " ")]
    public void Constructor_RejectsMissingVisibleText(
        string key,
        string status,
        string phrase,
        string source,
        string dismissLabel)
    {
        Assert.ThrowsAny<ArgumentException>(() => new OverlayNotification(
            key,
            status,
            phrase,
            source,
            dismissLabel));
    }

    [Fact]
    public void DisplayPolicy_UsesNormalizedFixedDuration()
    {
        var policy = new OverlayDisplayPolicy();
        policy.ApplySettings(new OverlayPresentationSettings
        {
            DurationMode = OverlayDurationMode.Fixed,
            FixedDurationSeconds = 12
        });
        var notification = new OverlayNotification(
            "tip",
            "Совет",
            "Короткий совет",
            "Источник",
            "Закрыть",
            OverlayNotificationTone.Coach);

        Assert.Equal(TimeSpan.FromSeconds(12), policy.GetLifetime(notification));
    }

    [Fact]
    public void PresentationSettings_ClampUntrustedValues()
    {
        var normalized = new OverlayPresentationSettings
        {
            Width = 900,
            BackgroundOpacity = 0.1,
            FixedDurationSeconds = 60,
            HorizontalPosition = -2,
            VerticalPosition = 5
        }.Normalize();

        Assert.Equal(400, normalized.Width);
        Assert.Equal(0.6, normalized.BackgroundOpacity);
        Assert.Equal(20, normalized.FixedDurationSeconds);
        Assert.Equal(0, normalized.HorizontalPosition);
        Assert.Equal(1, normalized.VerticalPosition);
    }
}
