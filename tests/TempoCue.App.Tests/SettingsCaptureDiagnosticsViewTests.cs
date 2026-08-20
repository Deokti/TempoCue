using System.Windows;
using System.Windows.Controls;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Проверяет присутствие и основные состояния карточки WGC-диагностики.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class SettingsCaptureDiagnosticsViewTests
{
    [Fact]
    public void CaptureCard_ShowsSnapshotAndAvailableActions()
    {
        WpfTestHost.Run(() =>
        {
            using var settingsContext = new TemporarySettingsContext();
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;
            var session = new TestWindowCaptureSession();
            Window? window = null;

            try
            {
                localization.SetLanguage("ru-RU");
                using var captureDiagnostics = new CaptureDiagnosticsViewModel(
                    session,
                    isSupported: true,
                    localization);
                using var viewModel = new SettingsViewModel(
                    localization,
                    settingsContext.Service,
                    static () => { },
                    captureDiagnostics: captureDiagnostics);
                var view = new SettingsView
                {
                    DataContext = viewModel,
                    Width = 820,
                    Height = 700
                };
                window = new Window
                {
                    Content = view,
                    Width = 860,
                    Height = 740,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                window.Show();
                WpfTestHost.Pump();

                var card = WpfTestHost.Find<Border>(view, "CaptureDiagnosticsCard");
                var selectButton = WpfTestHost.Find<Button>(view, "SelectCaptureWindowButton");
                var stopButton = WpfTestHost.Find<Button>(view, "StopCaptureButton");
                Assert.Equal(Visibility.Visible, card.Visibility);
                Assert.True(selectButton.IsEnabled);
                Assert.False(stopButton.IsEnabled);
                Assert.Equal("Выбрать окно игры", selectButton.Content);

                session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                    state: CaptureSessionState.Capturing,
                    targetName: "League of Legends",
                    frameWidth: 1920,
                    frameHeight: 1080,
                    receivedFrameCount: 12,
                    framesPerSecond: 60,
                    selectedFrameCount: 6,
                    selectionFramesPerSecond: 30,
                    rateLimitedFrameCount: 6,
                    lastFrameAt: DateTimeOffset.UtcNow,
                    minimapPreview: new MinimapPreviewSnapshot(
                        X: 1918,
                        Y: 1078,
                        Width: 2,
                        Height: 2,
                        Stride: 8,
                        BgraPixels: new byte[16],
                        CapturedAt: DateTimeOffset.UtcNow,
                        Corner: MinimapScreenCorner.BottomRight,
                        DetectionConfidence: 0.92)));
                captureDiagnostics.Refresh();
                WpfTestHost.Pump();

                Assert.Equal("Сменить окно", selectButton.Content);
                Assert.True(stopButton.IsEnabled);
                Assert.Equal("30,0", WpfTestHost.Find<TextBlock>(view, "SelectionFpsTextBlock").Text);
                Assert.Equal("6", WpfTestHost.Find<TextBlock>(view, "SelectedFramesTextBlock").Text);
                Assert.Equal("6", WpfTestHost.Find<TextBlock>(view, "RateLimitedFramesTextBlock").Text);
                Assert.Equal(
                    Visibility.Visible,
                    WpfTestHost.Find<Border>(view, "MinimapPreviewPanel").Visibility);
                Assert.NotNull(WpfTestHost.Find<Image>(view, "MinimapPreviewImage").Source);
            }
            finally
            {
                window?.Close();
                localization.SetLanguage(initialLanguage);
            }
        });
    }
}
