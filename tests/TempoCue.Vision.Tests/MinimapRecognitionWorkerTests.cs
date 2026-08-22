using System.Collections.Concurrent;

namespace TempoCue.Vision.Tests;

public sealed class MinimapRecognitionWorkerTests
{
    [Fact]
    public async Task TrySubmit_DropsStalePendingFrameWhileRecognitionIsBusy()
    {
        var recognizer = new BlockingRecognizer();
        var analyzer = new DefaultMinimapFrameAnalyzer(recognizer);
        var completedGenerations = new ConcurrentQueue<int>();
        var twoResultsReceived = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var worker = new MinimapRecognitionWorker(analyzer, result =>
        {
            completedGenerations.Enqueue(result.Generation);
            if (completedGenerations.Count == 2)
            {
                twoResultsReceived.TrySetResult();
            }
        });

        Assert.True(worker.TrySubmit(CreatePreview(1), [], 1));
        Assert.True(recognizer.WaitUntilFirstRecognitionStarts(TimeSpan.FromSeconds(5)));
        Assert.True(worker.TrySubmit(CreatePreview(2), [], 2));
        Assert.True(worker.TrySubmit(CreatePreview(3), [], 3));

        recognizer.ReleaseFirstRecognition();
        await twoResultsReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1, 3], completedGenerations);
    }

    private static MinimapPreviewSnapshot CreatePreview(int marker) =>
        new(
            0,
            0,
            1,
            1,
            1,
            1,
            4,
            [checked((byte)marker), 0, 0, 0],
            DateTimeOffset.UtcNow,
            MinimapScreenCorner.BottomRight,
            1);

    private sealed class BlockingRecognizer : IMinimapChampionRecognizer, IDisposable
    {
        private readonly ManualResetEventSlim _firstRecognitionStarted = new();
        private readonly ManualResetEventSlim _releaseFirstRecognition = new();
        private int _callCount;

        public MinimapChampionRecognitionResult Recognize(
            ReadOnlyMemory<byte> pixels,
            int width,
            int height,
            int stride,
            IReadOnlyList<MinimapChampionMarkerCandidate> proposals,
            IReadOnlyList<MinimapChampionReference> references)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                _firstRecognitionStarted.Set();
                _releaseFirstRecognition.Wait(TimeSpan.FromSeconds(5));
            }

            return new MinimapChampionRecognitionResult([], 0);
        }

        public bool WaitUntilFirstRecognitionStarts(TimeSpan timeout) =>
            _firstRecognitionStarted.Wait(timeout);

        public void ReleaseFirstRecognition() => _releaseFirstRecognition.Set();

        public void Dispose()
        {
            _releaseFirstRecognition.Set();
            _firstRecognitionStarted.Dispose();
            _releaseFirstRecognition.Dispose();
        }
    }
}
