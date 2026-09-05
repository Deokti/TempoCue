using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.ViewModels;

/// <summary>Представляет редкую диагностическую выборку из быстрого потока кадров.</summary>
public sealed class CaptureDiagnosticsViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);
    private readonly IWindowCaptureSession _captureSession;
    private readonly IActiveMatchVisionDiagnosticsSource? _visionDiagnostics;
    private readonly Func<bool>? _isMapObservationSwitchedOff;
    private readonly LocalizationService _localization;
    private readonly VisionParticipantStatusPresenter _participantStatusPresenter;
    private readonly DispatcherTimer _refreshTimer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CaptureDiagnosticsSnapshot _snapshot;
    private ActiveMatchVisionDiagnosticsSnapshot _visionSnapshot =
        ActiveMatchVisionDiagnosticsSnapshot.Empty;
    private ImageSource? _minimapPreviewImage;
    private IReadOnlyList<MinimapMarkerOverlayViewModel> _minimapMarkerOverlays =
        Array.Empty<MinimapMarkerOverlayViewModel>();
    private IReadOnlyList<VisionParticipantStatusViewModel> _allyParticipantStatuses =
        Array.Empty<VisionParticipantStatusViewModel>();
    private IReadOnlyList<VisionParticipantStatusViewModel> _enemyParticipantStatuses =
        Array.Empty<VisionParticipantStatusViewModel>();
    private DateTimeOffset? _renderedPreviewAt;
    private bool _isOperationRunning;
    private ReplaySampleExportState _replaySampleExportState;
    private bool _isDisposed;

    public CaptureDiagnosticsViewModel(
        IWindowCaptureSession captureSession,
        bool isSupported,
        LocalizationService localization)
        : this(captureSession, isSupported, localization, visionDiagnostics: null)
    {
    }

    /// <param name="isMapObservationSwitchedOff">
    /// Идёт ли матч, в котором наблюдать не для кого. Нужно только для
    /// подсказки: пустая диагностика во время игры выглядит поломкой, если не
    /// сказать, что это выключенные наборы наблюдения. ADR 0017.
    /// </param>
    internal CaptureDiagnosticsViewModel(
        IWindowCaptureSession captureSession,
        bool isSupported,
        LocalizationService localization,
        IActiveMatchVisionDiagnosticsSource? visionDiagnostics,
        Func<bool>? isMapObservationSwitchedOff = null)
    {
        _captureSession = captureSession ?? throw new ArgumentNullException(nameof(captureSession));
        _visionDiagnostics = visionDiagnostics;
        _isMapObservationSwitchedOff = isMapObservationSwitchedOff;
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _participantStatusPresenter = new VisionParticipantStatusPresenter(_localization);
        IsSupported = isSupported;
        _snapshot = captureSession.Snapshot;
        _localization.PropertyChanged += Localization_PropertyChanged;

        // UI получает только четыре снимка в секунду, даже если WGC отдаёт десятки кадров.
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
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

    public bool CanSaveReplaySample =>
        !_isOperationRunning &&
        !_isDisposed &&
        HasMinimapPreview &&
        _captureSession is IMinimapReplaySampleSource { HasReplaySample: true };

    public bool HasReplaySampleExportStatus =>
        _replaySampleExportState != ReplaySampleExportState.None;

    public string ReplaySampleExportStatusText => _localization.Get(
        _replaySampleExportState == ReplaySampleExportState.Saved
            ? "Settings.Capture.Replay.Status.Saved"
            : "Settings.Capture.Replay.Status.Failed");

    public string ReplaySampleDialogFilter =>
        _localization.Get("Settings.Capture.Replay.Filter");

    public string SequenceDialogFilter =>
        _localization.Get("Settings.Capture.Sequence.Filter");

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

    /// <summary>
    /// Наблюдение выключено самим игроком: все наборы наблюдения сняты, и
    /// захват намеренно не запускается.
    /// </summary>
    public bool IsMapObservationSwitchedOff =>
        !IsCapturing && _isMapObservationSwitchedOff?.Invoke() == true;

    /// <summary>
    /// Подпись под состоянием захвата. Обычно объясняет частоту обновления, но
    /// в матче без наборов наблюдения — почему захвата нет вовсе.
    /// </summary>
    public string StateHintText => _localization.Get(IsMapObservationSwitchedOff
        ? "Settings.Capture.State.Hint.ObservationOff"
        : "Settings.Capture.State.Hint");

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

    /// <summary>
    /// Ограничение частоты разбора — потолок, а не факт: дорогой кадр не даёт его
    /// достичь. Подпись называет потолок, значение — то, что получилось.
    /// </summary>
    public string RecognitionFramesPerSecondLabelText => string.Format(
        CultureInfo.CurrentCulture,
        _localization.Get("Settings.Capture.RecognitionFps.Label"),
        _snapshot.RecognitionFrameRateLimit);

    public string RecognitionFramesPerSecondText => _snapshot.RecognitionDurationMilliseconds > 0
        ? _snapshot.RecognitionFramesPerSecond.ToString("N1", CultureInfo.CurrentCulture)
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    public string RecognitionDurationText => _snapshot.RecognitionDurationMilliseconds > 0
        ? string.Format(
            CultureInfo.CurrentCulture,
            _localization.Get("Settings.Capture.RecognitionDuration.Value"),
            _snapshot.RecognitionDurationMilliseconds)
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    /// <summary>
    /// Разбор кадра подошёл к окну подтверждения появления. Число потоков этого не
    /// предскажет: та же беда случается на закреплённом за одним ядром процессе, в
    /// виртуальной машине и просто под нагрузкой, а <c>ProcessorCount</c> во всех
    /// этих случаях рапортует полную машину. Признак берётся из замера.
    /// Порог — половина окна: затянувшиеся кадры идут примерно в полтора раза
    /// дольше медианы, поэтому медиана выше половины уже роняет часть цепочек.
    /// </summary>
    public bool IsRecognitionTooSlowForConfirmation =>
        _snapshot.RecognitionDurationMilliseconds > 0 &&
        _snapshot.RecognitionDurationMilliseconds >
            ParticipantVisibilityTracker.DefaultMaximumDetectionGap.TotalMilliseconds / 2;

    /// <summary>
    /// Чем закончился запрос на захват без жёлтой рамки. Строка нужна затем, что
    /// иначе исчезнувшую или оставшуюся рамку не с чем связать: разрешено,
    /// отказано и «система не умеет» выглядят на экране одинаково.
    /// </summary>
    public string BorderlessCaptureText => _localization.Get(
        BorderlessCaptureAccess.State switch
        {
            BorderlessCaptureState.Allowed => "Settings.Capture.Borderless.Allowed",
            BorderlessCaptureState.Denied => "Settings.Capture.Borderless.Denied",
            BorderlessCaptureState.NotSupported => "Settings.Capture.Borderless.NotSupported",
            _ => "Settings.Capture.Borderless.Unknown"
        });

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

    public IReadOnlyList<MinimapMarkerOverlayViewModel> MinimapMarkerOverlays =>
        _minimapMarkerOverlays;

    public bool HasParticipantDiagnostics => _visionSnapshot.Participants.Count > 0;

    public string ActivePlayerTeamText => string.Format(
        CultureInfo.CurrentCulture,
        _localization.Get("Settings.Capture.Participants.ActiveTeam.Value"),
        _participantStatusPresenter.FormatRawTeam(_visionSnapshot.ActivePlayerTeam));

    public IReadOnlyList<VisionParticipantStatusViewModel> AllyParticipantStatuses =>
        _allyParticipantStatuses;

    public IReadOnlyList<VisionParticipantStatusViewModel> EnemyParticipantStatuses =>
        _enemyParticipantStatuses;

    public string MinimapMarkerCandidatesText
    {
        get
        {
            var candidates = _snapshot.MinimapPreview?.MarkerCandidates ??
                Array.Empty<MinimapMarkerCandidate>();
            var champions = _snapshot.MinimapPreview?.ChampionMarkerCandidates ??
                Array.Empty<MinimapChampionMarkerCandidate>();
            var preview = _snapshot.MinimapPreview;
            return string.Format(
                CultureInfo.CurrentCulture,
                _localization.Get("Settings.Capture.Minimap.Markers.Value"),
                candidates.Count,
                champions.Count,
                champions.Count(candidate =>
                    candidate.Affiliation == MinimapMarkerAffiliation.Ally),
                champions.Count(candidate =>
                    candidate.Affiliation == MinimapMarkerAffiliation.Enemy),
                preview?.ChampionProposalCount ?? 0,
                preview?.ChampionReferenceCount ?? 0,
                preview?.MaximumIdentityConfidence ?? 0);
        }
    }

    public string MinimapRegionText => _snapshot.MinimapPreview is { } preview
        ? string.Format(
            CultureInfo.CurrentCulture,
            _localization.Get("Settings.Capture.Minimap.Region.Value"),
            preview.RegionWidth,
            preview.RegionHeight,
            preview.X,
            preview.Y)
        : _localization.Get("Settings.Capture.Value.NotAvailable");

    public string MinimapNormalizedSizeText => _snapshot.MinimapPreview is { } preview
        ? string.Format(
            CultureInfo.CurrentCulture,
            _localization.Get("Settings.Capture.Minimap.Normalized.Value"),
            preview.ImageWidth,
            preview.ImageHeight)
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

    /// <summary>
    /// Сохраняет явную копию текущего кадра; фоновый захват сам ничего на диск не пишет.
    /// </summary>
    public async Task<bool> SaveReplaySampleAsync(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!CanSaveReplaySample ||
            _captureSession is not IMinimapReplaySampleSource replaySource)
        {
            return false;
        }

        var sample = replaySource.CreateReplaySample();
        if (sample is null)
        {
            SetReplaySampleExportState(ReplaySampleExportState.Failed);
            return false;
        }

        SetOperationRunning(true);
        try
        {
            await MinimapReplaySampleStore.SaveAsync(
                destination,
                sample,
                _lifetimeCancellation.Token);
            SetReplaySampleExportState(ReplaySampleExportState.Saved);
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            SetReplaySampleExportState(ReplaySampleExportState.Failed);
            return false;
        }
        finally
        {
            if (!_isDisposed)
            {
                SetOperationRunning(false);
            }
        }
    }

    public void ReportReplaySampleExportFailure() =>
        SetReplaySampleExportState(ReplaySampleExportState.Failed);

    /// <summary>
    /// Три секунды при восьми разборах в секунду. Этого хватает, чтобы увидеть,
    /// держится ли отметка на участнике или прыгает, и мало, чтобы игрок успел
    /// нажать в нужный момент и не отвлечься от матча.
    /// </summary>
    public const int SequenceFrameCount = 24;

    public bool CanRecordSequence =>
        !_isOperationRunning &&
        !_isDisposed &&
        IsCapturing &&
        _captureSession is IMinimapReplaySampleSource { IsRecordingSequence: false };

    public bool IsRecordingSequence =>
        _captureSession is IMinimapReplaySampleSource { IsRecordingSequence: true };

    public string SequenceRecordingStatusText => _captureSession is IMinimapReplaySampleSource source
        ? string.Format(
            CultureInfo.CurrentCulture,
            _localization.Get("Settings.Capture.Sequence.Progress"),
            source.RecordedFrameCount,
            SequenceFrameCount)
        : string.Empty;

    /// <summary>
    /// Начинает запись подряд идущих кадров. Одиночный кадр отвечает на вопрос
    /// «нашли ли участника», но не отвечает на вопрос «находим ли его на каждом
    /// кадре», а игроку видна именно вторая величина.
    /// </summary>
    public void StartSequenceRecording()
    {
        if (!CanRecordSequence ||
            _captureSession is not IMinimapReplaySampleSource replaySource)
        {
            return;
        }

        replaySource.StartSequenceRecording(SequenceFrameCount);
        OnPropertyChanged(nameof(CanRecordSequence));
        OnPropertyChanged(nameof(IsRecordingSequence));
        OnPropertyChanged(nameof(SequenceRecordingStatusText));
    }

    public bool HasRecordedSequence =>
        _captureSession is IMinimapReplaySampleSource source &&
        !source.IsRecordingSequence &&
        source.RecordedFrameCount > 0;

    public async Task<bool> SaveRecordedSequenceAsync(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (_captureSession is not IMinimapReplaySampleSource replaySource)
        {
            return false;
        }

        var sequence = replaySource.TakeRecordedSequence();
        if (sequence is null)
        {
            SetReplaySampleExportState(ReplaySampleExportState.Failed);
            return false;
        }

        SetOperationRunning(true);
        try
        {
            await MinimapReplaySequenceStore.SaveAsync(
                destination,
                sequence,
                _lifetimeCancellation.Token);
            SetReplaySampleExportState(ReplaySampleExportState.Saved);
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            SetReplaySampleExportState(ReplaySampleExportState.Failed);
            return false;
        }
        finally
        {
            if (!_isDisposed)
            {
                SetOperationRunning(false);
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
        var visionSnapshot = _visionDiagnostics?.DiagnosticsSnapshot ??
            ActiveMatchVisionDiagnosticsSnapshot.Empty;
        if (snapshot == _snapshot && ReferenceEquals(visionSnapshot, _visionSnapshot))
        {
            return;
        }

        _snapshot = snapshot;
        _visionSnapshot = visionSnapshot;
        UpdateMinimapPreviewImage();
        UpdateParticipantStatuses();
        NotifyDiagnosticsChanged();
    }

    internal void SetRefreshEnabled(bool isEnabled)
    {
        if (_isDisposed)
        {
            return;
        }

        if (isEnabled)
        {
            Refresh();
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
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
        OnPropertyChanged(nameof(IsMapObservationSwitchedOff));
        OnPropertyChanged(nameof(StateHintText));
        OnPropertyChanged(nameof(SelectButtonText));
        OnPropertyChanged(nameof(SelectedWindowText));
        OnPropertyChanged(nameof(ResolutionText));
        OnPropertyChanged(nameof(FramesPerSecondText));
        OnPropertyChanged(nameof(SelectionFramesPerSecondLabelText));
        OnPropertyChanged(nameof(SelectionFramesPerSecondText));
        OnPropertyChanged(nameof(RecognitionFramesPerSecondLabelText));
        OnPropertyChanged(nameof(RecognitionFramesPerSecondText));
        OnPropertyChanged(nameof(RecognitionDurationText));
        OnPropertyChanged(nameof(BorderlessCaptureText));
        OnPropertyChanged(nameof(IsRecognitionTooSlowForConfirmation));
        OnPropertyChanged(nameof(ReceivedFrameCountText));
        OnPropertyChanged(nameof(SelectedFrameCountText));
        OnPropertyChanged(nameof(RateLimitedFrameCountText));
        OnPropertyChanged(nameof(LastFrameText));
        OnPropertyChanged(nameof(ReplaySampleExportStatusText));
        OnPropertyChanged(nameof(MinimapRegionText));
        OnPropertyChanged(nameof(MinimapNormalizedSizeText));
        OnPropertyChanged(nameof(MinimapDetectionText));
        OnPropertyChanged(nameof(MinimapMarkerCandidatesText));
        UpdateParticipantStatuses();
        OnPropertyChanged(nameof(HasParticipantDiagnostics));
        OnPropertyChanged(nameof(ActivePlayerTeamText));
        OnPropertyChanged(nameof(AllyParticipantStatuses));
        OnPropertyChanged(nameof(EnemyParticipantStatuses));
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
        OnPropertyChanged(nameof(CanSaveReplaySample));
        OnPropertyChanged(nameof(CanRecordSequence));
        OnPropertyChanged(nameof(IsRecordingSequence));
        OnPropertyChanged(nameof(HasRecordedSequence));
        OnPropertyChanged(nameof(SequenceRecordingStatusText));
    }

    private void SetReplaySampleExportState(ReplaySampleExportState state)
    {
        if (_replaySampleExportState == state)
        {
            return;
        }

        _replaySampleExportState = state;
        OnPropertyChanged(nameof(HasReplaySampleExportStatus));
        OnPropertyChanged(nameof(ReplaySampleExportStatusText));
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
        OnPropertyChanged(nameof(CanSaveReplaySample));
        OnPropertyChanged(nameof(CanRecordSequence));
        OnPropertyChanged(nameof(IsRecordingSequence));
        OnPropertyChanged(nameof(HasRecordedSequence));
        OnPropertyChanged(nameof(SequenceRecordingStatusText));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsMapObservationSwitchedOff));
        OnPropertyChanged(nameof(StateHintText));
        OnPropertyChanged(nameof(SelectButtonText));
        OnPropertyChanged(nameof(SelectedWindowText));
        OnPropertyChanged(nameof(ResolutionText));
        OnPropertyChanged(nameof(FramesPerSecondText));
        OnPropertyChanged(nameof(SelectionFramesPerSecondLabelText));
        OnPropertyChanged(nameof(SelectionFramesPerSecondText));
        OnPropertyChanged(nameof(RecognitionFramesPerSecondLabelText));
        OnPropertyChanged(nameof(RecognitionFramesPerSecondText));
        OnPropertyChanged(nameof(RecognitionDurationText));
        OnPropertyChanged(nameof(BorderlessCaptureText));
        OnPropertyChanged(nameof(IsRecognitionTooSlowForConfirmation));
        OnPropertyChanged(nameof(ReceivedFrameCountText));
        OnPropertyChanged(nameof(SelectedFrameCountText));
        OnPropertyChanged(nameof(RateLimitedFrameCountText));
        OnPropertyChanged(nameof(LastFrameText));
        OnPropertyChanged(nameof(TechnicalErrorText));
        OnPropertyChanged(nameof(HasMinimapPreview));
        OnPropertyChanged(nameof(MinimapPreviewImage));
        OnPropertyChanged(nameof(MinimapMarkerOverlays));
        OnPropertyChanged(nameof(MinimapRegionText));
        OnPropertyChanged(nameof(MinimapNormalizedSizeText));
        OnPropertyChanged(nameof(MinimapDetectionText));
        OnPropertyChanged(nameof(MinimapMarkerCandidatesText));
        OnPropertyChanged(nameof(HasParticipantDiagnostics));
        OnPropertyChanged(nameof(ActivePlayerTeamText));
        OnPropertyChanged(nameof(AllyParticipantStatuses));
        OnPropertyChanged(nameof(EnemyParticipantStatuses));
        OnPropertyChanged(nameof(HasMinimapPreviewError));
        OnPropertyChanged(nameof(MinimapPreviewErrorText));
        OnPropertyChanged(nameof(HasReplaySampleExportStatus));
        OnPropertyChanged(nameof(ReplaySampleExportStatusText));
    }

    private void UpdateMinimapPreviewImage()
    {
        var preview = _snapshot.MinimapPreview;
        if (preview is null)
        {
            _renderedPreviewAt = null;
            _minimapPreviewImage = null;
            _minimapMarkerOverlays = Array.Empty<MinimapMarkerOverlayViewModel>();
            return;
        }

        if (_renderedPreviewAt == preview.CapturedAt)
        {
            return;
        }

        var presentation = MinimapPreviewPresentationFactory.Create(preview);
        _renderedPreviewAt = presentation.CapturedAt;
        _minimapPreviewImage = presentation.Image;
        _minimapMarkerOverlays = presentation.MarkerOverlays;
    }

    private void UpdateParticipantStatuses()
    {
        _allyParticipantStatuses = _participantStatusPresenter.Create(
            _visionSnapshot,
            MinimapMarkerAffiliation.Ally);
        _enemyParticipantStatuses = _participantStatusPresenter.Create(
            _visionSnapshot,
            MinimapMarkerAffiliation.Enemy);
    }

    private enum ReplaySampleExportState
    {
        None,
        Saved,
        Failed
    }
}
