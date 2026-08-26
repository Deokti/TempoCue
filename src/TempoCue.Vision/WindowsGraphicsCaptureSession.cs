using System.Diagnostics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace TempoCue.Vision;

/// <summary>Владеет одной диагностической сессией Windows Graphics Capture.</summary>
public sealed class WindowsGraphicsCaptureSession :
    IWindowCaptureSession,
    IMinimapChampionReferenceSink,
    IMinimapChampionObservationSource,
    IMinimapReplaySampleSource,
    IAsyncDisposable
{
    private const int FrameBufferCount = 2;
    private const int DefaultSelectionFrameRateLimit = 30;

    /// <summary>
    /// Частота разбора миникарты. Ограничение появилось для диагностического
    /// предпросмотра, когда распознавания ещё не было, и с тех пор определяет
    /// задержку игровых уведомлений: подтверждение видимости требует двух
    /// наблюдений, поэтому каждый лишний интервал добавляется к ней дважды.
    /// Восемь кадров в секунду сокращают задержку примерно на треть и остаются
    /// выше стоимости самого тяжёлого распознавания.
    /// </summary>
    private const int MinimapAnalysisFrameRateLimit = 8;
    private readonly object _frameSync = new();
    private readonly object _stateSync = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CaptureFrameMetrics _frameMetrics = new(
        DefaultSelectionFrameRateLimit,
        MinimapAnalysisFrameRateLimit);
    private readonly MinimapRecognitionWorker _recognitionWorker;
    private Direct3D11MinimapPreviewReader? _minimapPreviewReader;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _captureSession;
    private GraphicsCaptureItem? _captureItem;
    private IDirect3DDevice? _direct3DDevice;
    private CaptureSessionState _state = CaptureSessionState.Idle;
    private string? _targetName;
    private MinimapPreviewSnapshot? _minimapPreview;
    private MinimapChampionReference[] _minimapPreviewReferences = [];
    private readonly List<MinimapReplaySequenceFrame> _sequenceFrames = [];
    private MinimapChampionReference[]? _sequenceReferences;
    private int _sequenceTarget;
    private string? _minimapPreviewErrorMessage;
    private string? _errorMessage;
    private MinimapChampionReference[] _championReferences = [];
    private CancellationTokenSource? _automaticCaptureCancellation;
    private Task? _automaticCaptureTask;
    private int _generation;
    private int _disposeState;

    public event EventHandler<MinimapChampionObservationEventArgs>? ObservationAvailable;

    public WindowsGraphicsCaptureSession()
        : this(null)
    {
    }

    /// <summary>
    /// Позволяет composition root выбрать способ распознавания, не меняя захват.
    /// Значение по умолчанию — штатный конвейер на цветных контурах и эталонах.
    /// </summary>
    public WindowsGraphicsCaptureSession(IMinimapFrameAnalyzer? analyzer)
    {
        _recognitionWorker = new MinimapRecognitionWorker(
            analyzer ?? new DefaultMinimapFrameAnalyzer(),
            CompleteMinimapRecognition);
    }

    public CaptureDiagnosticsSnapshot Snapshot
    {
        get
        {
            var metrics = _frameMetrics.Snapshot;
            lock (_stateSync)
            {
                return new CaptureDiagnosticsSnapshot(
                    _state,
                    _targetName,
                    metrics.FrameWidth,
                    metrics.FrameHeight,
                    metrics.ReceivedFrameCount,
                    metrics.FramesPerSecond,
                    metrics.SelectionFrameRateLimit,
                    metrics.SelectedFrameCount,
                    metrics.SelectionFramesPerSecond,
                    metrics.RateLimitedFrameCount,
                    metrics.RecognitionFrameRateLimit,
                    metrics.RecognitionFramesPerSecond,
                    metrics.RecognitionDurationMilliseconds,
                    metrics.LastFrameAt,
                    _minimapPreview,
                    _minimapPreviewErrorMessage,
                    _errorMessage);
            }
        }
    }

    public void EnableAutomaticLeagueWindowCapture(
        Func<bool>? isMatchRunning = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        lock (_stateSync)
        {
            if (_automaticCaptureTask is { IsCompleted: false })
            {
                return;
            }

            _automaticCaptureCancellation?.Dispose();
            _automaticCaptureCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            var automaticCaptureToken = _automaticCaptureCancellation.Token;
            var gate = isMatchRunning ?? (static () => true);
            _automaticCaptureTask = Task.Run(
                () => MonitorLeagueWindowAsync(gate, automaticCaptureToken),
                automaticCaptureToken);
        }
    }

    public bool HasReplaySample
    {
        get
        {
            lock (_stateSync)
            {
                return _minimapPreview is not null;
            }
        }
    }

    public bool IsRecordingSequence
    {
        get
        {
            lock (_stateSync)
            {
                return _sequenceTarget > 0 && _sequenceFrames.Count < _sequenceTarget;
            }
        }
    }

    public int RecordedFrameCount
    {
        get
        {
            lock (_stateSync)
            {
                return _sequenceFrames.Count;
            }
        }
    }

    public void StartSequenceRecording(int frameCount)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            frameCount,
            MinimapReplaySequenceStore.MaximumFrameCount);

        lock (_stateSync)
        {
            _sequenceFrames.Clear();
            _sequenceReferences = null;
            _sequenceTarget = frameCount;
        }
    }

    public MinimapReplaySequence? TakeRecordedSequence()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        lock (_stateSync)
        {
            if (_sequenceFrames.Count == 0 || _sequenceReferences is null)
            {
                return null;
            }

            var sequence = new MinimapReplaySequence(
                MinimapFrameNormalizer.CanonicalSize,
                MinimapFrameNormalizer.CanonicalSize,
                checked(MinimapFrameNormalizer.CanonicalSize * 4),
                _sequenceFrames,
                _sequenceReferences);
            _sequenceFrames.Clear();
            _sequenceReferences = null;
            _sequenceTarget = 0;
            return sequence;
        }
    }

    /// <summary>
    /// Складывает разобранный кадр в запись, пока она не набрана. Вызывается из
    /// завершения распознавания, поэтому частота записи совпадает с частотой
    /// разбора — именно её устойчивость и требуется измерить.
    /// </summary>
    private void CollectSequenceFrame(MinimapPreviewSnapshot preview, MinimapChampionReference[] references)
    {
        lock (_stateSync)
        {
            if (_sequenceTarget <= 0 || _sequenceFrames.Count >= _sequenceTarget)
            {
                return;
            }

            _sequenceReferences ??= references;
            _sequenceFrames.Add(new MinimapReplaySequenceFrame(
                preview.CapturedAt,
                preview.BgraPixels.ToArray()));
        }
    }

    public async Task<bool> PickAndStartAsync(nint ownerWindow, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        ArgumentOutOfRangeException.ThrowIfZero(ownerWindow);
        if (!GraphicsCaptureSession.IsSupported())
        {
            SetState(CaptureSessionState.Unsupported, null);
            return false;
        }

        var generation = await BeginSelectionAsync(cancellationToken).ConfigureAwait(true);
        var picker = new GraphicsCapturePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, ownerWindow);

        GraphicsCaptureItem? item;
        try
        {
            item = await picker.PickSingleItemAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            await CompleteCancelledSelectionAsync(generation).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await CompleteFailedSelectionAsync(generation, exception.Message).ConfigureAwait(false);
            return false;
        }

        if (item is null)
        {
            await CompleteCancelledSelectionAsync(generation).ConfigureAwait(false);
            return false;
        }

        return await StartSelectedItemAsync(generation, item, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        _ = CancelAutomaticCapture();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _generation++;
            StopCaptureResources();
            SetState(CaptureSessionState.Stopped, null);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        var automaticCaptureTask = CancelAutomaticCapture();
        if (automaticCaptureTask is not null)
        {
            try
            {
                await automaticCaptureTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _generation++;
            StopCaptureResources();
        }
        finally
        {
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
            _automaticCaptureCancellation?.Dispose();
        }

        await _recognitionWorker.DisposeAsync().ConfigureAwait(false);
    }

    private async Task MonitorLeagueWindowAsync(
        Func<bool> isMatchRunning,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = Snapshot;
            var frameMetrics = _frameMetrics.Snapshot;
            var shouldRestart = AutomaticCaptureRecoveryPolicy.ShouldRestart(
                snapshot.State,
                frameMetrics.CaptureStartedAt,
                snapshot.LastFrameAt,
                DateTimeOffset.UtcNow);
            if (AutomaticCaptureRecoveryPolicy.ShouldAttach(
                    snapshot.State,
                    isMatchRunning(),
                    shouldRestart))
            {
                await TryStartLeagueWindowAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<bool> TryStartLeagueWindowAsync(CancellationToken cancellationToken)
    {
        if (!GraphicsCaptureSession.IsSupported())
        {
            SetState(CaptureSessionState.Unsupported, null);
            return false;
        }

        var windowHandle = LeagueWindowCaptureTarget.FindWindowHandle();
        if (windowHandle == nint.Zero)
        {
            await ReleaseMissingTargetAsync().ConfigureAwait(false);
            return false;
        }

        var generation = await BeginSelectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var item = GraphicsCaptureItemFactory.CreateForWindow(windowHandle);
            return await StartSelectedItemAsync(
                generation,
                item,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await CompleteCancelledSelectionAsync(generation).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await CompleteFailedSelectionAsync(generation, exception.Message).ConfigureAwait(false);
            return false;
        }
    }

    private Task? CancelAutomaticCapture()
    {
        lock (_stateSync)
        {
            _automaticCaptureCancellation?.Cancel();
            return _automaticCaptureTask;
        }
    }

    private async Task<int> BeginSelectionAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var generation = ++_generation;
            StopCaptureResources();
            _frameMetrics.Reset();
            SetState(CaptureSessionState.SelectingTarget, null);
            return generation;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task<bool> StartSelectedItemAsync(
        int generation,
        GraphicsCaptureItem item,
        CancellationToken cancellationToken)
    {
        IDirect3DDevice? device = null;
        Direct3D11CaptureFramePool? framePool = null;
        GraphicsCaptureSession? captureSession = null;
        try
        {
            device = Direct3D11DeviceFactory.Create();
            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                FrameBufferCount,
                item.Size);
            captureSession = framePool.CreateCaptureSession(item);
            captureSession.IsCursorCaptureEnabled = false;
            // Жёлтую рамку вокруг окна рисует система. Убрать её можно только с
            // разрешения пользователя, и оно спрашивается один раз при запуске.
            BorderlessCaptureAccess.TryHideBorder(captureSession);

            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _disposeState) != 0 || generation != _generation)
                {
                    return false;
                }

                _direct3DDevice = device;
                _framePool = framePool;
                _captureSession = captureSession;
                _captureItem = item;
                device = null;
                framePool = null;
                captureSession = null;
                lock (_stateSync)
                {
                    _targetName = item.DisplayName;
                }
                _frameMetrics.SetInitialFrameSize(item.Size.Width, item.Size.Height);
                _framePool.FrameArrived += FramePool_FrameArrived;
                _captureItem.Closed += CaptureItem_Closed;
                _captureSession.StartCapture();
                _frameMetrics.MarkCaptureStarted(DateTimeOffset.UtcNow);
                SetState(CaptureSessionState.Capturing, null);
                return true;
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            await CompleteCancelledSelectionAsync(generation).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await CompleteFailedSelectionAsync(generation, exception.Message).ConfigureAwait(false);
            return false;
        }
        finally
        {
            captureSession?.Dispose();
            framePool?.Dispose();
            (device as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Возвращает сессию в исходное состояние, когда окно матча исчезло: освобождает
    /// ресурсы захвата и обнуляет метрики, чтобы диагностика показывала «окно не
    /// выбрано», а не замершие цифры прошлого матча.
    /// </summary>
    private async Task ReleaseMissingTargetAsync()
    {
        if (!AutomaticCaptureRecoveryPolicy.ShouldReleaseMissingTarget(Snapshot.State))
        {
            return;
        }

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!AutomaticCaptureRecoveryPolicy.ShouldReleaseMissingTarget(Snapshot.State))
            {
                return;
            }

            _generation++;
            StopCaptureResources();
            _frameMetrics.Reset();
            SetState(CaptureSessionState.Idle, null);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task CompleteCancelledSelectionAsync(int generation)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (generation == _generation)
            {
                SetState(CaptureSessionState.Idle, null);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task CompleteFailedSelectionAsync(int generation, string message)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (generation == _generation)
            {
                StopCaptureResources();
                SetState(CaptureSessionState.Failed, message);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void FramePool_FrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            lock (_frameSync)
            {
                if (!ReferenceEquals(sender, _framePool))
                {
                    return;
                }

                var contentWidth = 0;
                var contentHeight = 0;
                var decision = default(CaptureFrameDecision);
                using (var frame = sender.TryGetNextFrame())
                {
                    if (frame is null)
                    {
                        return;
                    }

                    contentWidth = frame.ContentSize.Width;
                    contentHeight = frame.ContentSize.Height;
                    decision = _frameMetrics.RegisterFrame(
                        contentWidth,
                        contentHeight,
                        Stopwatch.GetTimestamp(),
                        DateTimeOffset.UtcNow);
                    if (decision.ShouldCreatePreview)
                    {
                        TryQueueMinimapPreview(
                            frame.Surface,
                            contentWidth,
                            contentHeight);
                    }
                }

                if (contentWidth <= 0 || contentHeight <= 0)
                {
                    return;
                }

                if (decision.IsSizeChanged && _direct3DDevice is not null)
                {
                    sender.Recreate(
                        _direct3DDevice,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        FrameBufferCount,
                        new Windows.Graphics.SizeInt32(contentWidth, contentHeight));
                }
            }
        }
        catch (Exception exception)
        {
            SetState(CaptureSessionState.Failed, exception.Message);
        }
    }

    private void CaptureItem_Closed(GraphicsCaptureItem sender, object args) =>
        _ = StopAfterTargetClosedAsync();

    private async Task StopAfterTargetClosedAsync()
    {
        try
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _generation++;
                StopCaptureResources();
                SetState(CaptureSessionState.Stopped, null);
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // Закрытие приложения уже завершило владельца сессии.
        }
    }

    private void StopCaptureResources()
    {
        lock (_frameSync)
        {
            if (_framePool is not null)
            {
                _framePool.FrameArrived -= FramePool_FrameArrived;
            }

            if (_captureItem is not null)
            {
                _captureItem.Closed -= CaptureItem_Closed;
            }

            _captureSession?.Dispose();
            _framePool?.Dispose();
            (_direct3DDevice as IDisposable)?.Dispose();
            _captureSession = null;
            _framePool = null;
            _captureItem = null;
            _direct3DDevice = null;
            _frameMetrics.ClearCaptureStarted();
            _minimapPreviewReader?.Dispose();
            _minimapPreviewReader = null;
            lock (_stateSync)
            {
                _targetName = null;
                _minimapPreview = null;
                _minimapPreviewReferences = [];
                _minimapPreviewErrorMessage = null;
            }
        }
    }

    public void SetChampionReferences(IReadOnlyList<MinimapChampionReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

        Volatile.Write(ref _championReferences, references.ToArray());
    }

    public MinimapReplaySample? CreateReplaySample()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        lock (_stateSync)
        {
            return _minimapPreview is null
                ? null
                : MinimapReplaySample.FromPreview(
                    _minimapPreview,
                    _minimapPreviewReferences);
        }
    }

    private void TryQueueMinimapPreview(
        IDirect3DSurface surface,
        int contentWidth,
        int contentHeight)
    {
        try
        {
            _minimapPreviewReader ??= new Direct3D11MinimapPreviewReader();
            var championReferences = Volatile.Read(ref _championReferences);
            var preview = _minimapPreviewReader.Read(
                surface,
                contentWidth,
                contentHeight,
                DateTimeOffset.UtcNow);
            _recognitionWorker.TrySubmit(preview, championReferences, _generation);
        }
        catch (Exception exception)
        {
            lock (_stateSync)
            {
                _minimapPreviewErrorMessage = exception.Message;
            }

            // Неудачное чтение — тоже тик времени: подписчик должен засчитать пропуск
            // кадра, иначе удержание видимости не истечёт при потерянной миникарте.
            // Признак неуспеха не даёт ошибке выдавать поток за работоспособный.
            PublishObservation(new MinimapChampionObservationEventArgs(
                DateTimeOffset.UtcNow,
                MinimapFrameNormalizer.CanonicalSize,
                MinimapFrameNormalizer.CanonicalSize,
                [],
                isSuccessful: false));
        }
    }

    private void CompleteMinimapRecognition(RecognitionWorkResult result)
    {
        if (result.Generation != Volatile.Read(ref _generation) ||
            Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

        // Учитывается и неудачный разбор: он тоже занимает поток и тоже определяет,
        // с какой частотой приходят наблюдения.
        _frameMetrics.RegisterRecognition(result.Duration, Stopwatch.GetTimestamp());

        if (result.ErrorMessage is not null)
        {
            lock (_stateSync)
            {
                _minimapPreviewErrorMessage = result.ErrorMessage;
            }

            PublishObservation(new MinimapChampionObservationEventArgs(
                result.Preview.CapturedAt,
                result.Preview.ImageWidth,
                result.Preview.ImageHeight,
                [],
                isSuccessful: false));
            return;
        }

        lock (_stateSync)
        {
            _minimapPreview = result.Preview;
            _minimapPreviewReferences = result.ChampionReferences;
            _minimapPreviewErrorMessage = null;
        }

        CollectSequenceFrame(result.Preview, result.ChampionReferences);

        PublishObservation(new MinimapChampionObservationEventArgs(
            result.Preview.CapturedAt,
            result.Preview.ImageWidth,
            result.Preview.ImageHeight,
            result.Preview.ChampionMarkerCandidates));
    }

    private void PublishObservation(MinimapChampionObservationEventArgs args)
    {
        var handlers = ObservationAvailable?.GetInvocationList();
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.Cast<EventHandler<MinimapChampionObservationEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception)
            {
                // Ошибка потребителя наблюдений не должна останавливать WGC и поток кадров.
                Trace.TraceError(
                    "Обработчик наблюдения миникарты завершился ошибкой ({0}): {1}",
                    exception.GetType().Name,
                    exception.Message);
            }
        }
    }

    private void SetState(CaptureSessionState state, string? errorMessage)
    {
        lock (_stateSync)
        {
            _state = state;
            _errorMessage = errorMessage;
        }
    }
}
