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

                var captureView = WpfTestHost.Find<CaptureDiagnosticsView>(
                    view,
                    "CaptureDiagnosticsView");
                var card = FindCaptureElement<Border>(captureView, "CaptureDiagnosticsCard");
                var selectButton = FindCaptureElement<Button>(
                    captureView,
                    "SelectCaptureWindowButton");
                var stopButton = FindCaptureElement<Button>(captureView, "StopCaptureButton");
                var saveReplayButton = FindCaptureElement<Button>(
                    captureView,
                    "SaveReplaySampleButton");
                Assert.Equal(Visibility.Visible, card.Visibility);
                Assert.True(selectButton.IsEnabled);
                Assert.False(stopButton.IsEnabled);
                Assert.False(saveReplayButton.IsEnabled);
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
                        RegionWidth: 452,
                        RegionHeight: 452,
                        ImageWidth: 320,
                        ImageHeight: 320,
                        ImageStride: 1280,
                        BgraPixels: new byte[320 * 320 * 4],
                        CapturedAt: DateTimeOffset.UtcNow,
                        Corner: MinimapScreenCorner.BottomRight,
                        DetectionConfidence: 0.92)
                    {
                        MarkerCandidates =
                        [
                            new MinimapMarkerCandidate(
                                20,
                                30,
                                12,
                                12,
                                MinimapMarkerAffiliation.Ally,
                                0.8)
                        ],
                        ChampionMarkerCandidates =
                        [
                            new MinimapChampionMarkerCandidate(
                                20,
                                30,
                                12,
                                12,
                                MinimapMarkerAffiliation.Ally,
                                0.65)
                        ]
                    }));
                session.SetReplaySample(MinimapReplaySample.FromPreview(
                    Assert.IsType<MinimapPreviewSnapshot>(session.Snapshot.MinimapPreview),
                    []));
                captureDiagnostics.Refresh();
                WpfTestHost.Pump();

                Assert.Equal("Сменить окно", selectButton.Content);
                Assert.True(stopButton.IsEnabled);
                Assert.True(saveReplayButton.IsEnabled);
                Assert.Equal(
                    "30,0",
                    FindCaptureElement<TextBlock>(captureView, "SelectionFpsTextBlock").Text);
                Assert.Equal(
                    "6",
                    FindCaptureElement<TextBlock>(captureView, "SelectedFramesTextBlock").Text);
                Assert.Equal(
                    "6",
                    FindCaptureElement<TextBlock>(captureView, "RateLimitedFramesTextBlock").Text);
                Assert.Equal(
                    "320 × 320 пкс · фиксированная геометрия",
                    FindCaptureElement<TextBlock>(
                        captureView,
                        "MinimapNormalizedSizeTextBlock").Text);
                Assert.Equal(
                    Visibility.Visible,
                    FindCaptureElement<Border>(captureView, "MinimapPreviewPanel").Visibility);
                Assert.NotNull(
                    FindCaptureElement<Image>(captureView, "MinimapPreviewImage").Source);
                Assert.Equal(
                    "Цветовых объектов: 1 · областей сравнения: 0 · подтверждено портретами: 1 (союзных: 1, вражеских: 0) · эталонов: 0 · лучший балл: 0 %",
                    FindCaptureElement<TextBlock>(
                        captureView,
                        "MinimapMarkerCandidatesTextBlock").Text);
                Assert.Single(
                    FindCaptureElement<ItemsControl>(
                        captureView,
                        "MinimapMarkerCandidatesItemsControl").Items);
            }
            finally
            {
                window?.Close();
                localization.SetLanguage(initialLanguage);
            }
        });
    }

    private static T FindCaptureElement<T>(CaptureDiagnosticsView view, string name)
        where T : FrameworkElement =>
        WpfTestHost.Find<T>(view, name);
}
