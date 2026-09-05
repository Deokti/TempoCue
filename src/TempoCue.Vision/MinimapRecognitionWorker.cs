using System.Diagnostics;
using System.Threading.Channels;

namespace TempoCue.Vision;

/// <summary>
/// Выполняет тяжёлое распознавание вне callback Windows Graphics Capture.
/// В очереди хранится только последний ожидающий кадр, чтобы задержка не накапливалась.
/// </summary>
internal sealed class MinimapRecognitionWorker : IAsyncDisposable
{
    private readonly Channel<RecognitionWorkItem> _pendingFrames;
    private readonly Func<IMinimapFrameAnalyzer> _analyzerFactory;
    private readonly Action<RecognitionWorkResult> _completed;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _processingTask;

    /// <summary>
    /// Фабрика создаёт распознаватель при первом кадре. Готовый экземпляр
    /// принадлежит worker сразу, даже если до остановки не поступило кадров.
    /// </summary>
    private IMinimapFrameAnalyzer? _analyzer;
    private int _disposeState;

    public MinimapRecognitionWorker(Action<RecognitionWorkResult> completed)
        : this(static () => new DefaultMinimapFrameAnalyzer(), completed)
    {
    }

    public MinimapRecognitionWorker(
        IMinimapFrameAnalyzer analyzer,
        Action<RecognitionWorkResult> completed)
        : this(
            analyzer is null
                ? throw new ArgumentNullException(nameof(analyzer))
                : () => analyzer,
            completed)
    {
        _analyzer = analyzer;
    }

    /// <param name="analyzerFactory">
    /// Откладывает создание распознавателя до первого кадра. Веса модели — это
    /// мегабайты памяти и рабочие потоки ONNX Runtime, и держать их с запуска
    /// приложения незачем: пока наблюдение за картой выключено или матч не
    /// начался, кадров не будет вовсе. ADR 0017.
    /// </param>
    public MinimapRecognitionWorker(
        Func<IMinimapFrameAnalyzer> analyzerFactory,
        Action<RecognitionWorkResult> completed)
    {
        _analyzerFactory = analyzerFactory
            ?? throw new ArgumentNullException(nameof(analyzerFactory));
        _completed = completed ?? throw new ArgumentNullException(nameof(completed));
        _pendingFrames = Channel.CreateBounded<RecognitionWorkItem>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        _processingTask = Task.Run(ProcessAsync);
    }

    public bool TrySubmit(
        MinimapPreviewSnapshot preview,
        MinimapChampionReference[] championReferences,
        int generation)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(championReferences);

        return Volatile.Read(ref _disposeState) == 0 &&
            _pendingFrames.Writer.TryWrite(
                new RecognitionWorkItem(preview, championReferences, generation));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _pendingFrames.Writer.TryComplete();
        _stopping.Cancel();
        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _stopping.Dispose();
            (_analyzer as IDisposable)?.Dispose();
        }
    }

    private async Task ProcessAsync()
    {
        await foreach (var item in _pendingFrames.Reader.ReadAllAsync(_stopping.Token)
            .ConfigureAwait(false))
        {
            RecognitionWorkResult result;
            // Секундомер запускается после создания распознавателя: однократная
            // загрузка весов не должна попасть в метрику времени разбора кадра
            // и выглядеть там медленным распознаванием.
            var stopwatch = new Stopwatch();
            try
            {
                var analyzer = _analyzer ??= _analyzerFactory();
                stopwatch.Start();
                var recognition = analyzer.Analyze(
                    item.Preview.BgraPixels,
                    item.Preview.ImageWidth,
                    item.Preview.ImageHeight,
                    item.Preview.ImageStride,
                    item.ChampionReferences);
                var recognizedPreview = item.Preview with
                {
                    MarkerCandidates = recognition.MarkerCandidates.ToArray(),
                    ChampionProposalCount = recognition.ProposalCount,
                    ChampionMarkerCandidates = recognition.ChampionCandidates,
                    ChampionReferenceCount = item.ChampionReferences.Length,
                    MaximumIdentityConfidence = recognition.MaximumIdentityConfidence
                };
                result = new RecognitionWorkResult(
                    recognizedPreview,
                    item.ChampionReferences,
                    item.Generation,
                    stopwatch.Elapsed,
                    null);
            }
            catch (Exception exception)
            {
                result = new RecognitionWorkResult(
                    item.Preview,
                    item.ChampionReferences,
                    item.Generation,
                    stopwatch.Elapsed,
                    exception.Message);
            }

            if (!_stopping.IsCancellationRequested)
            {
                _completed(result);
            }
        }
    }

    private sealed record RecognitionWorkItem(
        MinimapPreviewSnapshot Preview,
        MinimapChampionReference[] ChampionReferences,
        int Generation);
}

/// <param name="Duration">
/// Сколько заняло распознавание кадра. Показывается в диагностике: без этого
/// числа настройка числа потоков остаётся слепой.
/// </param>
internal sealed record RecognitionWorkResult(
    MinimapPreviewSnapshot Preview,
    MinimapChampionReference[] ChampionReferences,
    int Generation,
    TimeSpan Duration,
    string? ErrorMessage);
