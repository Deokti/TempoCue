using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace TempoCue.Vision;

/// <summary>Владеет одной диагностической сессией Windows Graphics Capture.</summary>
public sealed class WindowsGraphicsCaptureSession : IWindowCaptureSession, IAsyncDisposable
{
    private const int FrameBufferCount = 2;
    private const int DefaultSelectionFrameRateLimit = 30;
    private readonly object _frameSync = new();
    private readonly object _stateSync = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly FrameRateEstimator _frameRate = new();
    private readonly FrameSelectionGate _frameSelection = new(DefaultSelectionFrameRateLimit);
    private readonly FrameRateEstimator _selectionFrameRate = new();
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _captureSession;
    private GraphicsCaptureItem? _captureItem;
    private IDirect3DDevice? _direct3DDevice;
    private CaptureSessionState _state = CaptureSessionState.Idle;
    private string? _targetName;
    private int _frameWidth;
    private int _frameHeight;
    private long _receivedFrameCount;
    private long _selectedFrameCount;
    private long _rateLimitedFrameCount;
    private long _lastFrameUtcTicks;
    private string? _errorMessage;
    private int _generation;
    private int _disposeState;

    public CaptureDiagnosticsSnapshot Snapshot
    {
        get
        {
            var lastFrameTicks = Interlocked.Read(ref _lastFrameUtcTicks);
            lock (_stateSync)
            {
                return new CaptureDiagnosticsSnapshot(
                    _state,
                    _targetName,
                    Volatile.Read(ref _frameWidth),
                    Volatile.Read(ref _frameHeight),
                    Interlocked.Read(ref _receivedFrameCount),
                    _frameRate.FramesPerSecond,
                    _frameSelection.MaximumFramesPerSecond,
                    Interlocked.Read(ref _selectedFrameCount),
                    _selectionFrameRate.FramesPerSecond,
                    Interlocked.Read(ref _rateLimitedFrameCount),
                    lastFrameTicks == 0 ? null : new DateTimeOffset(lastFrameTicks, TimeSpan.Zero),
                    _errorMessage);
            }
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
        }
    }

    private async Task<int> BeginSelectionAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var generation = ++_generation;
            StopCaptureResources();
            ResetMetrics();
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
                _frameWidth = item.Size.Width;
                _frameHeight = item.Size.Height;
                _framePool.FrameArrived += FramePool_FrameArrived;
                _captureItem.Closed += CaptureItem_Closed;
                _captureSession.StartCapture();
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
                using (var frame = sender.TryGetNextFrame())
                {
                    if (frame is null)
                    {
                        return;
                    }

                    contentWidth = frame.ContentSize.Width;
                    contentHeight = frame.ContentSize.Height;
                    var timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    Interlocked.Increment(ref _receivedFrameCount);
                    Interlocked.Exchange(ref _lastFrameUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
                    _frameRate.RegisterFrame(timestamp);
                    if (_frameSelection.TrySelect(timestamp))
                    {
                        Interlocked.Increment(ref _selectedFrameCount);
                        _selectionFrameRate.RegisterFrame(timestamp);
                    }
                    else
                    {
                        Interlocked.Increment(ref _rateLimitedFrameCount);
                    }
                }

                if (contentWidth <= 0 || contentHeight <= 0)
                {
                    return;
                }

                var sizeChanged = contentWidth != Volatile.Read(ref _frameWidth) ||
                    contentHeight != Volatile.Read(ref _frameHeight);
                Volatile.Write(ref _frameWidth, contentWidth);
                Volatile.Write(ref _frameHeight, contentHeight);
                if (sizeChanged && _direct3DDevice is not null)
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
            lock (_stateSync)
            {
                _targetName = null;
            }
        }
    }

    private void ResetMetrics()
    {
        Volatile.Write(ref _frameWidth, 0);
        Volatile.Write(ref _frameHeight, 0);
        Interlocked.Exchange(ref _receivedFrameCount, 0);
        Interlocked.Exchange(ref _selectedFrameCount, 0);
        Interlocked.Exchange(ref _rateLimitedFrameCount, 0);
        Interlocked.Exchange(ref _lastFrameUtcTicks, 0);
        _frameRate.Reset();
        _frameSelection.Reset();
        _selectionFrameRate.Reset();
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
