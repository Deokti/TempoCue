using System.IO;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>
/// Запись последовательности — единственный способ измерить устойчивость
/// распознавания между кадрами. Если кнопка доступна не тогда, когда надо, или
/// запись не доходит до файла, измерять будет нечего.
/// </summary>
[Collection(LocalizationCollection.Name)]
public sealed class CaptureDiagnosticsSequenceTests
{
    [Fact]
    public void RecordSequence_RequiresActiveCapture()
    {
        WpfTestHost.Run(() =>
        {
            var session = new TestWindowCaptureSession();
            using var viewModel = new CaptureDiagnosticsViewModel(
                session,
                isSupported: true,
                LocalizationService.Instance);

            // Без идущего захвата записывать нечего: кадры не поступают.
            viewModel.Refresh();
            Assert.False(viewModel.CanRecordSequence);

            session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                state: CaptureSessionState.Capturing));
            viewModel.Refresh();

            Assert.True(viewModel.CanRecordSequence);
        });
    }

    [Fact]
    public void StartRecording_AsksSessionForThreeSecondsOfFrames()
    {
        WpfTestHost.Run(() =>
        {
            var session = new TestWindowCaptureSession();
            using var viewModel = new CaptureDiagnosticsViewModel(
                session,
                isSupported: true,
                LocalizationService.Instance);
            session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                state: CaptureSessionState.Capturing));
            viewModel.Refresh();

            viewModel.StartSequenceRecording();

            Assert.Equal(
                CaptureDiagnosticsViewModel.SequenceFrameCount,
                session.RequestedSequenceFrames);
            // Пока запись идёт, повторный запуск обнулил бы уже собранное.
            Assert.True(viewModel.IsRecordingSequence);
            Assert.False(viewModel.CanRecordSequence);
        });
    }

    [Fact]
    public void SaveRecordedSequence_WritesReadableFile()
    {
        WpfTestHost.Run(() =>
        {
            var session = new TestWindowCaptureSession();
            using var viewModel = new CaptureDiagnosticsViewModel(
                session,
                isSupported: true,
                LocalizationService.Instance);
            session.SetSnapshot(TestCaptureDiagnosticsSnapshot.Create(
                state: CaptureSessionState.Capturing));
            viewModel.Refresh();
            session.CompleteSequenceRecording(CreateSequence());

            using var destination = new MemoryStream();
            var saved = viewModel.SaveRecordedSequenceAsync(destination).GetAwaiter().GetResult();

            Assert.True(saved);
            destination.Position = 0;
            var loaded = MinimapReplaySequenceStore.LoadAsync(destination).GetAwaiter().GetResult();
            Assert.Equal(3, loaded.Frames.Count);

            // Запись забирается один раз: иначе тот же файл сохранится дважды и
            // будет выглядеть как два независимых наблюдения.
            Assert.False(viewModel.HasRecordedSequence);
        });
    }

    private static MinimapReplaySequence CreateSequence()
    {
        const int side = MinimapFrameNormalizer.CanonicalSize;
        const int stride = side * 4;
        var start = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
        var frames = Enumerable.Range(0, 3)
            .Select(index => new MinimapReplaySequenceFrame(
                start.AddMilliseconds(125 * index),
                new byte[stride * side]))
            .ToArray();
        return new MinimapReplaySequence(side, side, stride, frames, []);
    }
}
