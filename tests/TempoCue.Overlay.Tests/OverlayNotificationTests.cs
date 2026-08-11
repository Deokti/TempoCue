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
            "  новое  ");

        Assert.Equal("matchup.rule.1", notification.Key);
        Assert.Equal("Совет", notification.Status);
        Assert.Equal("Не вступайте в бой", notification.Phrase);
        Assert.Equal("Набор советов · Urgot против Sett", notification.Source);
        Assert.Equal("Закрыть оверлей", notification.DismissLabel);
        Assert.Equal(OverlayNotificationTone.Coach, notification.Tone);
        Assert.Equal("уровни 1–8", notification.Context);
        Assert.Equal("новое", notification.Badge);
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
}
