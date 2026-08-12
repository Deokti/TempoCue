using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TempoCue.Overlay.Tests;

public sealed class OverlayHostTests
{
    private const double ExpectedTopMargin = 80;

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

        AssertWindowBehavior(window);
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

    private static void AssertWindowBehavior(OverlayWindow window)
    {
        Assert.True(window.IsVisible);
        Assert.False(window.ShowInTaskbar);
        Assert.False(window.ShowActivated);
        Assert.True(window.Topmost);
        Assert.Equal(SystemParameters.WorkArea.Top + ExpectedTopMargin, window.Top);
    }

    [Fact]
    public void ShowPreview_SupportsManualPrioritySequenceAndRestart() => WpfTestHost.Run(() =>
    {
        using var host = new OverlayHost(Dispatcher.CurrentDispatcher);
        var warning = Create(
            "settings.preview.warning",
            "Тестовое предупреждение",
            OverlayNotificationTone.Caution);
        var information = Create("settings.preview.information", "Тестовое событие");
        var danger = Create(
            "settings.preview.danger",
            "Тестовая опасность",
            OverlayNotificationTone.Danger);

        host.SetInteractionMode(OverlayInteractionMode.Setup);
        host.ShowPreview([warning]);
        WpfTestHost.Pump();
        var window = Assert.IsType<OverlayWindow>(host.CurrentWindow);
        Assert.Same(warning, window.PrimaryCardView.DataContext);

        host.Show(information);
        WpfTestHost.Pump();
        Assert.Same(information, window.SecondaryCardView.DataContext);

        host.Show(danger);
        WpfTestHost.Pump();
        Assert.Same(danger, window.PrimaryCardView.DataContext);
        Assert.Equal(
            "settings.preview.danger",
            Assert.Single(host.CurrentSnapshot.Items).Notification.Key);

        host.ShowPreview([warning]);
        WpfTestHost.Pump();
        Assert.Same(warning, window.PrimaryCardView.DataContext);
        Assert.Equal(
            "settings.preview.warning",
            Assert.Single(host.CurrentSnapshot.Items).Notification.Key);
        host.Hide();
    });

    [Fact]
    public void Settings_ApplyWidthOpacityAndAllowSetupWhileGameOverlayIsDisabled() => WpfTestHost.Run(() =>
    {
        using var host = new OverlayHost(Dispatcher.CurrentDispatcher);
        host.ApplySettings(new OverlayPresentationSettings
        {
            IsEnabled = false,
            Width = 400,
            BackgroundOpacity = 0.65
        });

        host.Show(Create("game", "Игровое уведомление"));
        Assert.Null(host.CurrentWindow);

        host.SetInteractionMode(OverlayInteractionMode.Setup);
        host.ShowPreview([Create("preview", "Предпросмотр")]);
        WpfTestHost.Pump();
        var window = Assert.IsType<OverlayWindow>(host.CurrentWindow);
        var card = Assert.IsType<Border>(window.PrimaryCardView.FindName("OverlayCard"));
        var background = Assert.IsType<SolidColorBrush>(card.Background);

        Assert.Equal(400, window.Width);
        Assert.Equal((byte)166, background.Color.A);
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
                new OverlayDisplayItem(
                    2,
                    Create(
                        "caution",
                        "Не рассчитывай на безопасный recall",
                        OverlayNotificationTone.Coach,
                        "Urgot против Sett",
                        "Уровень 2",
                        "Подсказка по матчапу"),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddSeconds(13)),
                new OverlayDisplayItem(1, Create("info", "Дракон появится через 30 секунд", OverlayNotificationTone.Information), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(5))
            ]));
            window.Show();
            WpfTestHost.Pump(DispatcherPriority.Loaded);
            var primaryCard = Assert.IsType<Border>(window.PrimaryCardView.FindName("OverlayCard"));
            var primaryBadge = Assert.IsType<Border>(window.PrimaryCardView.FindName("OverlayBadgeBorder"));
            var primaryPhrase = Assert.IsType<TextBlock>(window.PrimaryCardView.FindName("OverlayPhraseTextBlock"));
            var primarySource = Assert.IsType<TextBlock>(window.PrimaryCardView.FindName("OverlaySourceTextBlock"));

            Assert.InRange(primaryCard.ActualWidth, 338, 342);
            Assert.Equal(Visibility.Visible, primaryBadge.Visibility);
            Assert.InRange(primaryBadge.ActualHeight, 16, 24);
            Assert.True(primaryPhrase.ActualHeight >= primaryPhrase.FontSize);
            Assert.True(primarySource.ActualHeight >= primarySource.FontSize);
            Assert.True(
                primaryBadge.TranslatePoint(new Point(), primaryCard).Y <
                primaryPhrase.TranslatePoint(new Point(), primaryCard).Y);
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
        OverlayNotificationTone tone = OverlayNotificationTone.Information,
        string? context = "только что",
        string? badge = null,
        string? status = null) => new(
        key,
        status ?? tone switch
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
        context,
        badge);
}
