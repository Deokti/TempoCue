using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TempoCue.Overlay.Tests;

public sealed class OverlayHostTests
{
    [Fact]
    public void Show_ReusesWindowAndReplacesEqualPriorityCard() => WpfTestHost.Run(() =>
    {
        using var host = new OverlayHost(Dispatcher.CurrentDispatcher);
        var first = Create("first", "Первый совет");
        var second = Create("second", "Второй совет");

        host.SetInteractionMode(OverlayInteractionMode.Setup);
        host.Show(first);
        WpfTestHost.Pump();
        var window = Assert.IsType<OverlayWindow>(host.CurrentWindow);

        Assert.True(window.IsVisible);
        Assert.False(window.ShowInTaskbar);
        Assert.False(window.ShowActivated);
        Assert.True(window.Topmost);
        Assert.Same(first, window.PrimaryCardView.DataContext);

        host.Show(second);
        WpfTestHost.Pump();

        Assert.Same(window, host.CurrentWindow);
        Assert.Same(second, window.PrimaryCardView.DataContext);
        Assert.Equal(
            second.Phrase,
            Assert.IsType<TextBlock>(window.PrimaryCardView.FindName("OverlayPhraseTextBlock")).Text);

        host.Hide();
    });

    [Fact]
    public void ShowPreview_CanBeRepeatedImmediatelyAfterHide() => WpfTestHost.Run(() =>
    {
        using var host = new OverlayHost(Dispatcher.CurrentDispatcher);
        var preview = Create("settings.preview", "Тестовый совет");

        host.SetInteractionMode(OverlayInteractionMode.Setup);
        host.ShowPreview(preview);
        WpfTestHost.Pump();
        var window = Assert.IsType<OverlayWindow>(host.CurrentWindow);
        Assert.Same(preview, window.PrimaryCardView.DataContext);

        host.Hide();
        host.ShowPreview(preview);
        WpfTestHost.Pump();

        Assert.True(window.IsVisible);
        Assert.Same(preview, window.PrimaryCardView.DataContext);
        host.Hide();
    });

    [Fact]
    public void Preview_RendersTwoCardsWithoutClippedTextAndCanSaveSnapshot() => WpfTestHost.Run(() =>
    {
        var policy = new OverlayDisplayPolicy();
        var window = new OverlayWindow(policy);
        try
        {
            window.SetInteractionMode(OverlayInteractionMode.Setup);
            window.ShowNotifications(new OverlayScheduleSnapshot(
            [
                new OverlayDisplayItem(2, Create("caution", "Возможная угроза", OverlayNotificationTone.Caution), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(6)),
                new OverlayDisplayItem(1, Create("info", "Дракон появится через 30 секунд", OverlayNotificationTone.Information), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(5))
            ]));
            window.Show();
            WpfTestHost.Pump(DispatcherPriority.Loaded);
            var primaryCard = Assert.IsType<Border>(window.PrimaryCardView.FindName("OverlayCard"));
            var primaryPhrase = Assert.IsType<TextBlock>(window.PrimaryCardView.FindName("OverlayPhraseTextBlock"));
            var primarySource = Assert.IsType<TextBlock>(window.PrimaryCardView.FindName("OverlaySourceTextBlock"));

            Assert.InRange(primaryCard.ActualWidth, 338, 342);
            Assert.True(primaryPhrase.ActualHeight >= primaryPhrase.FontSize);
            Assert.True(primarySource.ActualHeight >= primarySource.FontSize);
            Assert.Equal(Visibility.Visible, window.SecondaryCardView.Visibility);

            var snapshotDirectory = Environment.GetEnvironmentVariable("TEMPOCUE_UI_SNAPSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(snapshotDirectory))
            {
                WpfTestHost.SavePng(
                    window.OverlayCardsPanel,
                    Path.Combine(snapshotDirectory, "overlay-two-cards-ru.png"));
            }
        }
        finally
        {
            window.Close();
        }
    });

    private static OverlayNotification Create(
        string key,
        string phrase,
        OverlayNotificationTone tone = OverlayNotificationTone.Information) => new(
        key,
        tone switch
        {
            OverlayNotificationTone.Danger => "Опасно",
            OverlayNotificationTone.Caution => "Осторожно",
            OverlayNotificationTone.Coach => "Совет",
            OverlayNotificationTone.NoData => "Нет данных",
            OverlayNotificationTone.Paused => "Пауза",
            _ => "Событие"
        },
        phrase,
        "Набор советов · тест",
        "Закрыть оверлей",
        tone,
        "только что");
}
