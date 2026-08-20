using System.Globalization;
using TempoCue.App.Localization;
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
                session.SetSnapshot(new CaptureDiagnosticsSnapshot(
                    CaptureSessionState.Capturing,
                    "League of Legends (TM) Client",
                    1920,
                    1080,
                    42,
                    59.5,
                    new DateTimeOffset(2026, 8, 20, 7, 15, 30, 125, TimeSpan.Zero),
                    null));

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

            session.SetSnapshot(new CaptureDiagnosticsSnapshot(
                CaptureSessionState.Capturing,
                "League",
                1280,
                720,
                1,
                0,
                DateTimeOffset.UtcNow,
                null));
            viewModel.Refresh();
            viewModel.StopAsync().GetAwaiter().GetResult();

            Assert.Equal(1, session.StopCount);
        });
    }
}
