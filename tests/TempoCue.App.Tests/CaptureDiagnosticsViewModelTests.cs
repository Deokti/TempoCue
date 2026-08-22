using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.App.ViewModels;
using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Проверяет проекцию диагностического снимка захвата без обращения к WGC.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CaptureDiagnosticsViewModelTests
{
    [Fact]
    public void Refresh_ProjectsCapturingSnapshotIntoLocalizedDiagnostics()
    {
        WpfTestHost.Run(() =>
        {
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;
            var session = new TestWindowCaptureSession();

            try
            {
                localization.SetLanguage("ru-RU");
                using var viewModel = new CaptureDiagnosticsViewModel(
                    session,
                    isSupported: true,
                    localization);
                session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                    state: CaptureSessionState.Capturing,
                    targetName: "League of Legends (TM) Client",
                    frameWidth: 1920,
                    frameHeight: 1080,
                    receivedFrameCount: 42,
                    framesPerSecond: 59.5,
                    selectedFrameCount: 21,
                    selectionFramesPerSecond: 29.8,
                    rateLimitedFrameCount: 21,
                    lastFrameAt: new DateTimeOffset(
                        2026,
                        8,
                        20,
                        7,
                        15,
                        30,
                        125,
                        TimeSpan.Zero),
                    minimapPreview: CreatePreview()));

                viewModel.Refresh();

                Assert.True(viewModel.IsCapturing);
                Assert.False(viewModel.IsFailed);
                Assert.True(viewModel.CanStop);
                Assert.Equal("Кадры поступают", viewModel.StateText);
                Assert.Equal("Сменить окно", viewModel.SelectButtonText);
                Assert.Equal("League of Legends (TM) Client", viewModel.SelectedWindowText);
                Assert.Equal("1920 × 1080", viewModel.ResolutionText);
                Assert.Equal(59.5.ToString("N1", CultureInfo.CurrentCulture), viewModel.FramesPerSecondText);
                Assert.Equal(42L.ToString("N0", CultureInfo.CurrentCulture), viewModel.ReceivedFrameCountText);
                Assert.Equal("FPS отбора (лимит 30)", viewModel.SelectionFramesPerSecondLabelText);
                Assert.Equal(29.8.ToString("N1", CultureInfo.CurrentCulture), viewModel.SelectionFramesPerSecondText);
                Assert.Equal(21L.ToString("N0", CultureInfo.CurrentCulture), viewModel.SelectedFrameCountText);
                Assert.Equal(21L.ToString("N0", CultureInfo.CurrentCulture), viewModel.RateLimitedFrameCountText);
                Assert.True(viewModel.HasMinimapPreview);
                Assert.Equal("452 × 452 пкс · x 1918, y 1078", viewModel.MinimapRegionText);
                Assert.Equal(
                    "320 × 320 пкс · фиксированная геометрия",
                    viewModel.MinimapNormalizedSizeText);
                Assert.Equal("справа внизу · уверенность 92 %", viewModel.MinimapDetectionText);
                Assert.Equal(
                    "Цветовых объектов: 2 · областей сравнения: 0 · подтверждено портретами: 1 (союзных: 0, вражеских: 1) · эталонов: 0 · лучший балл: 0 %",
                    viewModel.MinimapMarkerCandidatesText);
                Assert.Single(viewModel.MinimapMarkerOverlays);
                var previewImage = Assert.IsAssignableFrom<BitmapSource>(
                    viewModel.MinimapPreviewImage);
                Assert.Equal(320, previewImage.PixelWidth);
                Assert.Equal(320, previewImage.PixelHeight);
                Assert.False(viewModel.HasTechnicalError);
            }
            finally
            {
                localization.SetLanguage(initialLanguage);
            }
        });
    }

    [Fact]
    public void SelectAndStop_DelegateActionsToCaptureSession()
    {
        WpfTestHost.Run(() =>
        {
            var session = new TestWindowCaptureSession();
            using var viewModel = new CaptureDiagnosticsViewModel(
                session,
                isSupported: true,
                LocalizationService.Instance);

            viewModel.SelectWindowAsync((nint)123).GetAwaiter().GetResult();
            Assert.Equal(1, session.PickCount);
            Assert.Equal((nint)123, session.LastOwnerWindow);

            session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                state: CaptureSessionState.Capturing,
                targetName: "League",
                frameWidth: 1280,
                frameHeight: 720,
                receivedFrameCount: 1,
                lastFrameAt: DateTimeOffset.UtcNow));
            viewModel.Refresh();
            viewModel.StopAsync().GetAwaiter().GetResult();

            Assert.Equal(1, session.StopCount);
        });
    }

    [Fact]
    public void SaveReplaySample_WritesCurrentDiagnosticFrame()
    {
        WpfTestHost.Run(() =>
        {
            var preview = CreatePreview();
            var session = new TestWindowCaptureSession();
            using var viewModel = new CaptureDiagnosticsViewModel(
                session,
                isSupported: true,
                LocalizationService.Instance);
            session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                state: CaptureSessionState.Capturing,
                minimapPreview: preview));
            session.SetReplaySample(MinimapReplaySample.FromPreview(preview, []));
            viewModel.Refresh();
            using var destination = new MemoryStream();

            var saved = viewModel.SaveReplaySampleAsync(destination)
                .GetAwaiter()
                .GetResult();

            Assert.True(saved);
            Assert.True(destination.Length > 0);
            Assert.True(viewModel.HasReplaySampleExportStatus);
            Assert.True(viewModel.CanSaveReplaySample);
        });
    }

    [Fact]
    public void Refresh_ShowsActualAnalysisRateInsteadOfRepeatingTheLimit()
    {
        WpfTestHost.Run(() =>
        {
            var localization = LocalizationService.Instance;
            var initialLanguage = localization.CurrentLanguageCode;
            var session = new TestWindowCaptureSession();

            try
            {
                localization.SetLanguage("ru-RU");
                using var viewModel = new CaptureDiagnosticsViewModel(
                    session,
                    isSupported: true,
                    localization);

                // До первого разбора показывать нечего: ноль миллисекунд означал бы
                // мгновенное распознавание, а на деле его ещё не было.
                viewModel.Refresh();
                Assert.Equal("Нет данных", viewModel.RecognitionDurationText);
                Assert.Equal("Нет данных", viewModel.RecognitionFramesPerSecondText);

                session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                    state: CaptureSessionState.Capturing,
                    recognitionFrameRateLimit: 8,
                    recognitionFramesPerSecond: 5.4,
                    recognitionDurationMilliseconds: 184.2));
                viewModel.Refresh();

                // Подпись называет потолок, значение — то, что получилось. Если
                // разбор дороже интервала, эти числа обязаны расходиться.
                Assert.Equal("FPS разбора карты (лимит 8)", viewModel.RecognitionFramesPerSecondLabelText);
                Assert.Equal(
                    5.4.ToString("N1", CultureInfo.CurrentCulture),
                    viewModel.RecognitionFramesPerSecondText);
                Assert.Equal("184 мс", viewModel.RecognitionDurationText);
                Assert.False(viewModel.IsRecognitionTooSlowForConfirmation);

                // Признак берётся из замера, а не из числа потоков: закреплённый за
                // одним ядром процесс всё равно рапортует полную машину.
                session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                    state: CaptureSessionState.Capturing,
                    recognitionFrameRateLimit: 8,
                    recognitionFramesPerSecond: 2.1,
                    recognitionDurationMilliseconds:
                        ParticipantVisibilityTracker.DefaultMaximumDetectionGap.TotalMilliseconds));
                viewModel.Refresh();

                Assert.True(viewModel.IsRecognitionTooSlowForConfirmation);
            }
            finally
            {
                localization.SetLanguage(initialLanguage);
            }
        });
    }

    private static MinimapPreviewSnapshot CreatePreview() =>
        new(
            X: 1918,
            Y: 1078,
            RegionWidth: 452,
            RegionHeight: 452,
            ImageWidth: 320,
            ImageHeight: 320,
            ImageStride: 1280,
            BgraPixels: new byte[320 * 320 * 4],
            CapturedAt: new DateTimeOffset(2026, 8, 20, 7, 15, 30, TimeSpan.Zero),
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
                    0.8),
                new MinimapMarkerCandidate(
                    180,
                    120,
                    14,
                    14,
                    MinimapMarkerAffiliation.Enemy,
                    0.75)
            ],
            ChampionMarkerCandidates =
            [
                new MinimapChampionMarkerCandidate(
                    180,
                    120,
                    14,
                    14,
                    MinimapMarkerAffiliation.Enemy,
                    0.68)
            ]
        };
}
