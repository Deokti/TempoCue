namespace TempoCue.Vision;

/// <summary>Потокобезопасно учитывает нативные, отобранные и диагностические кадры.</summary>
internal sealed class CaptureFrameMetrics
{
    private readonly FrameRateEstimator _receivedFrameRate = new();
    private readonly FrameSelectionGate _frameSelection;
    private readonly FrameRateEstimator _selectionFrameRate = new();
    private readonly FrameSelectionGate _previewSelection;
    private int _frameWidth;
    private int _frameHeight;
    private long _receivedFrameCount;
    private long _selectedFrameCount;
    private long _rateLimitedFrameCount;
    private long _lastFrameUtcTicks;
    private long _captureStartedUtcTicks;

    public CaptureFrameMetrics(
        int selectionFrameRateLimit,
        int previewFrameRateLimit)
    {
        _frameSelection = new FrameSelectionGate(selectionFrameRateLimit);
        _previewSelection = new FrameSelectionGate(previewFrameRateLimit);
    }

    public CaptureFrameMetricsSnapshot Snapshot
    {
        get
        {
            var lastFrameTicks = Interlocked.Read(ref _lastFrameUtcTicks);
            var captureStartedTicks = Interlocked.Read(ref _captureStartedUtcTicks);
            return new CaptureFrameMetricsSnapshot(
                Volatile.Read(ref _frameWidth),
                Volatile.Read(ref _frameHeight),
                Interlocked.Read(ref _receivedFrameCount),
                _receivedFrameRate.FramesPerSecond,
                _frameSelection.MaximumFramesPerSecond,
                Interlocked.Read(ref _selectedFrameCount),
                _selectionFrameRate.FramesPerSecond,
                Interlocked.Read(ref _rateLimitedFrameCount),
                lastFrameTicks == 0
                    ? null
                    : new DateTimeOffset(lastFrameTicks, TimeSpan.Zero),
                captureStartedTicks == 0
                    ? null
                    : new DateTimeOffset(captureStartedTicks, TimeSpan.Zero));
        }
    }

    public CaptureFrameDecision RegisterFrame(
        int width,
        int height,
        long timestamp,
        DateTimeOffset capturedAt)
    {
        Interlocked.Increment(ref _receivedFrameCount);
        Interlocked.Exchange(ref _lastFrameUtcTicks, capturedAt.UtcTicks);
        _receivedFrameRate.RegisterFrame(timestamp);

        var selected = _frameSelection.TrySelect(timestamp);
        if (selected)
        {
            Interlocked.Increment(ref _selectedFrameCount);
            _selectionFrameRate.RegisterFrame(timestamp);
        }
        else
        {
            Interlocked.Increment(ref _rateLimitedFrameCount);
        }

        var sizeChanged = width > 0 && height > 0 &&
            (width != Volatile.Read(ref _frameWidth) ||
             height != Volatile.Read(ref _frameHeight));
        if (width > 0 && height > 0)
        {
            Volatile.Write(ref _frameWidth, width);
            Volatile.Write(ref _frameHeight, height);
        }

        return new CaptureFrameDecision(
            selected,
            selected && _previewSelection.TrySelect(timestamp),
            sizeChanged);
    }

    public void SetInitialFrameSize(int width, int height)
    {
        Volatile.Write(ref _frameWidth, width);
        Volatile.Write(ref _frameHeight, height);
    }

    public void MarkCaptureStarted(DateTimeOffset startedAt) =>
        Interlocked.Exchange(ref _captureStartedUtcTicks, startedAt.UtcTicks);

    public void ClearCaptureStarted() =>
        Interlocked.Exchange(ref _captureStartedUtcTicks, 0);

    public void Reset()
    {
        Volatile.Write(ref _frameWidth, 0);
        Volatile.Write(ref _frameHeight, 0);
        Interlocked.Exchange(ref _receivedFrameCount, 0);
        Interlocked.Exchange(ref _selectedFrameCount, 0);
        Interlocked.Exchange(ref _rateLimitedFrameCount, 0);
        Interlocked.Exchange(ref _lastFrameUtcTicks, 0);
        Interlocked.Exchange(ref _captureStartedUtcTicks, 0);
        _receivedFrameRate.Reset();
        _frameSelection.Reset();
        _selectionFrameRate.Reset();
        _previewSelection.Reset();
    }
}

internal sealed record CaptureFrameMetricsSnapshot(
    int FrameWidth,
    int FrameHeight,
    long ReceivedFrameCount,
    double FramesPerSecond,
    int SelectionFrameRateLimit,
    long SelectedFrameCount,
    double SelectionFramesPerSecond,
    long RateLimitedFrameCount,
    DateTimeOffset? LastFrameAt,
    DateTimeOffset? CaptureStartedAt);

internal readonly record struct CaptureFrameDecision(
    bool IsSelected,
    bool ShouldCreatePreview,
    bool IsSizeChanged);
