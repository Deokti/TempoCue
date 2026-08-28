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

    [Fact]
    public async Task Analyzer_IsNotCreatedUntilTheFirstFrameArrives()
    {
        var created = 0;
        var firstResult = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var worker = new MinimapRecognitionWorker(
            () =>
            {
                Interlocked.Increment(ref created);
                return new CountingAnalyzer();
            },
            _ => firstResult.TrySetResult());

        // Пока кадров нет, веса модели никому не нужны: без наблюдения за картой
        // их не будет вовсе, а стоят они памяти и рабочих потоков.
        Assert.Equal(0, Volatile.Read(ref created));

        Assert.True(worker.TrySubmit(CreatePreview(1), [], 1));
        await firstResult.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, Volatile.Read(ref created));
    }

    [Fact]
    public async Task Analyzer_IsCreatedOnceForAllFrames()
    {
        var created = 0;
        var secondResult = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = 0;
        await using var worker = new MinimapRecognitionWorker(
            () =>
            {
                Interlocked.Increment(ref created);
                return new CountingAnalyzer();
            },
            _ =>
            {
                if (Interlocked.Increment(ref completed) == 2)
                {
                    secondResult.TrySetResult();
                }
            });

        Assert.True(worker.TrySubmit(CreatePreview(1), [], 1));
        await Task.Delay(50);
        Assert.True(worker.TrySubmit(CreatePreview(2), [], 2));
        await secondResult.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Создание сессии ONNX Runtime дороже самого разбора кадра, и повторять
        // его на каждом кадре означало бы поменять одну трату на другую, худшую.
        Assert.Equal(1, Volatile.Read(ref created));
    }

    [Fact]
    public async Task Analyzer_IsDisposedOnlyWhenItWasCreated()
    {
        var analyzer = new CountingAnalyzer();
        await using (var worker = new MinimapRecognitionWorker(
            () => analyzer,
            static _ => { }))
        {
        }

        // Несозданный распознаватель нечего освобождать, и попытка не должна
        // создать его ради этого.
        Assert.False(analyzer.IsDisposed);
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

    private sealed class CountingAnalyzer : IMinimapFrameAnalyzer, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public MinimapFrameAnalysis Analyze(
            ReadOnlyMemory<byte> pixels,
            int width,
            int height,
            int stride,
            IReadOnlyList<MinimapChampionReference> championReferences) =>
            new([], 0, [], 0);

        public void Dispose() => IsDisposed = true;
    }
}
