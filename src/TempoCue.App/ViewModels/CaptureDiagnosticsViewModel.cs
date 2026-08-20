using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TempoCue.App.Localization;
using TempoCue.Vision;

namespace TempoCue.App.ViewModels;

/// <summary>Представляет редкую диагностическую выборку из быстрого потока кадров.</summary>
public sealed class CaptureDiagnosticsViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);
    private readonly IWindowCaptureSession _captureSession;
    private readonly LocalizationService _localization;
    private readonly DispatcherTimer _refreshTimer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CaptureDiagnosticsSnapshot _snapshot;
    private ImageSource? _minimapPreviewImage;
    private DateTimeOffset? _renderedPreviewAt;
    private bool _isOperationRunning;
    private bool _isDisposed;

    public CaptureDiagnosticsViewModel(
        IWindowCaptureSession captureSession,
        bool isSupported,
        LocalizationService localization)
    {
        _captureSession = captureSession ?? throw new ArgumentNullException(nameof(captureSession));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        IsSupported = isSupported;
        _snapshot = captureSession.Snapshot;
        _localization.PropertyChanged += Localization_PropertyChanged;

        // UI получает только четыре снимка в секунду, даже если WGC отдаёт десятки кадров.
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
    }

    public bool IsSupported { get; }

    public CaptureSessionState State => _snapshot.State;

    public bool IsCapturing => State is CaptureSessionState.Capturing;

    public bool IsSelectingTarget => State is CaptureSessionState.SelectingTarget;

    public bool IsFailed => State is CaptureSessionState.Failed;

    public bool IsUnsupported => !IsSupported || State is CaptureSessionState.Unsupported;

    public bool HasTechnicalError =>
        State is CaptureSessionState.Failed && !string.IsNullOrWhiteSpace(_snapshot.ErrorMessage);

    public bool CanSelectWindow => IsSupported && !_isOperationRunning && !_isDisposed;

    public bool CanStop => IsCapturing && !_isOperationRunning && !_isDisposed;

    public string StateText => _localization.Get(IsUnsupported
        ? "Settings.Capture.State.Unsupported"
        : State switch
        {
            CaptureSessionState.SelectingTarget => "Settings.Capture.State.Selecting",
            CaptureSessionState.Capturing => "Settings.Capture.State.Capturing",
            CaptureSessionState.Stopped => "Settings.Capture.State.Stopped",
            CaptureSessionState.Failed => "Settings.Capture.State.Failed",
            _ => "Settings.Capture.State.Idle"
        });

    public string SelectButtonText => _localization.Get(IsCapturing
        ? "Settings.Capture.Change.Button"
        : "Settings.Capture.Select.Button");

    public string SelectedWindowText => string.IsNullOrWhiteSpace(_snapshot.TargetName)
        ? _localization.Get("Settings.Capture.Value.NotAvailable")
        : _snapshot.TargetName;

    public string ResolutionText => _snapshot.FrameWidth > 0 && _snapshot.FrameHeight > 0
        ? $"{_snapshot.FrameWidth} × {_snapshot.FrameHeight}"
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    public string FramesPerSecondText => _snapshot.ReceivedFrameCount > 0
        ? _snapshot.FramesPerSecond.ToString("N1", CultureInfo.CurrentCulture)
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    public string SelectionFramesPerSecondLabelText => string.Format(
        CultureInfo.CurrentCulture,
        _localization.Get("Settings.Capture.SelectionFps.Label"),
        _snapshot.SelectionFrameRateLimit);

    public string SelectionFramesPerSecondText => _snapshot.SelectedFrameCount > 0
        ? _snapshot.SelectionFramesPerSecond.ToString("N1", CultureInfo.CurrentCulture)
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    public string ReceivedFrameCountText =>
        _snapshot.ReceivedFrameCount.ToString("N0", CultureInfo.CurrentCulture);

    public string SelectedFrameCountText =>
        _snapshot.SelectedFrameCount.ToString("N0", CultureInfo.CurrentCulture);

    public string RateLimitedFrameCountText =>
        _snapshot.RateLimitedFrameCount.ToString("N0", CultureInfo.CurrentCulture);

    public string LastFrameText => _snapshot.LastFrameAt is DateTimeOffset lastFrame
        ? lastFrame.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture)
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    public string TechnicalErrorText => _snapshot.ErrorMessage ?? string.Empty;

    public bool HasMinimapPreview =>
        _snapshot.MinimapPreview is not null && _minimapPreviewImage is not null;

    public ImageSource? MinimapPreviewImage => _minimapPreviewImage;

    public string MinimapRegionText => _snapshot.MinimapPreview is { } preview
        ? string.Format(
            CultureInfo.CurrentCulture,
            _localization.Get("Settings.Capture.Minimap.Region.Value"),
            preview.Width,
            preview.Height,
            preview.X,
            preview.Y)
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    public string MinimapDetectionText => _snapshot.MinimapPreview is { } preview
        ? string.Format(
            CultureInfo.CurrentCulture,
            _localization.Get("Settings.Capture.Minimap.Detection.Value"),
            _localization.Get(preview.Corner == MinimapScreenCorner.BottomRight
                ? "Settings.Capture.Minimap.Corner.Right"
                : "Settings.Capture.Minimap.Corner.Left"),
            preview.DetectionConfidence)
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    public bool HasMinimapPreviewError =>
        !string.IsNullOrWhiteSpace(_snapshot.MinimapPreviewErrorMessage);

    public string MinimapPreviewErrorText =>
        _snapshot.MinimapPreviewErrorMessage ?? string.Empty;

    public async Task SelectWindowAsync(nint ownerWindow)
    {
        if (!CanSelectWindow)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfZero(ownerWindow);
        SetOperationRunning(true);
        try
        {
            await _captureSession.PickAndStartAsync(
                ownerWindow,
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Закрытие приложения отменяет ожидающий системный выбор окна.
        }
        finally
        {
            if (!_isDisposed)
            {
                SetOperationRunning(false);
                Refresh();
            }
        }
    }

    public async Task StopAsync()
    {
        if (!CanStop)
        {
            return;
        }

        SetOperationRunning(true);
        try
        {
            await _captureSession.StopAsync(_lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Общая отмена означает завершение владельца, а не ошибку пользователя.
        }
        finally
        {
            if (!_isDisposed)
            {
                SetOperationRunning(false);
                Refresh();
            }
        }
    }

    /// <summary>Считывает очередной неизменяемый снимок без доступа к самому кадру.</summary>
    public void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var snapshot = _captureSession.Snapshot;
        if (snapshot == _snapshot)
        {
            return;
        }

        _snapshot = snapshot;
        UpdateMinimapPreviewImage();
        NotifyDiagnosticsChanged();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _localization.PropertyChanged -= Localization_PropertyChanged;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e) => Refresh();

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(LocalizationService.CurrentLanguageCode) and not "Item[]")
        {
            return;
        }

        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(SelectButtonText));
        OnPropertyChanged(nameof(SelectedWindowText));
        OnPropertyChanged(nameof(ResolutionText));
        OnPropertyChanged(nameof(FramesPerSecondText));
        OnPropertyChanged(nameof(SelectionFramesPerSecondLabelText));
        OnPropertyChanged(nameof(SelectionFramesPerSecondText));
        OnPropertyChanged(nameof(ReceivedFrameCountText));
        OnPropertyChanged(nameof(SelectedFrameCountText));
        OnPropertyChanged(nameof(RateLimitedFrameCountText));
        OnPropertyChanged(nameof(LastFrameText));
        OnPropertyChanged(nameof(MinimapRegionText));
        OnPropertyChanged(nameof(MinimapDetectionText));
    }

    private void SetOperationRunning(bool value)
    {
        if (_isOperationRunning == value)
        {
            return;
        }

        _isOperationRunning = value;
        OnPropertyChanged(nameof(CanSelectWindow));
        OnPropertyChanged(nameof(CanStop));
    }

    private void NotifyDiagnosticsChanged()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsCapturing));
        OnPropertyChanged(nameof(IsSelectingTarget));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsUnsupported));
        OnPropertyChanged(nameof(HasTechnicalError));
        OnPropertyChanged(nameof(CanSelectWindow));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(SelectButtonText));
        OnPropertyChanged(nameof(SelectedWindowText));
        OnPropertyChanged(nameof(ResolutionText));
        OnPropertyChanged(nameof(FramesPerSecondText));
        OnPropertyChanged(nameof(SelectionFramesPerSecondLabelText));
        OnPropertyChanged(nameof(SelectionFramesPerSecondText));
        OnPropertyChanged(nameof(ReceivedFrameCountText));
        OnPropertyChanged(nameof(SelectedFrameCountText));
        OnPropertyChanged(nameof(RateLimitedFrameCountText));
        OnPropertyChanged(nameof(LastFrameText));
        OnPropertyChanged(nameof(TechnicalErrorText));
        OnPropertyChanged(nameof(HasMinimapPreview));
        OnPropertyChanged(nameof(MinimapPreviewImage));
        OnPropertyChanged(nameof(MinimapRegionText));
        OnPropertyChanged(nameof(MinimapDetectionText));
        OnPropertyChanged(nameof(HasMinimapPreviewError));
        OnPropertyChanged(nameof(MinimapPreviewErrorText));
    }

    private void UpdateMinimapPreviewImage()
    {
        var preview = _snapshot.MinimapPreview;
        if (preview is null)
        {
            _renderedPreviewAt = null;
            _minimapPreviewImage = null;
            return;
        }

        if (_renderedPreviewAt == preview.CapturedAt)
        {
            return;
        }

        var image = BitmapSource.Create(
            preview.Width,
            preview.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            preview.BgraPixels,
            preview.Stride);
        image.Freeze();
        _renderedPreviewAt = preview.CapturedAt;
        _minimapPreviewImage = image;
    }
}
