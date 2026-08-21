using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.ViewModels;

/// <summary>Представляет редкую диагностическую выборку из быстрого потока кадров.</summary>
public sealed class CaptureDiagnosticsViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);
    private static readonly Brush AllyMarkerBrush = CreateFrozenBrush(80, 183, 237);
    private static readonly Brush EnemyMarkerBrush = CreateFrozenBrush(255, 89, 116);
    private static readonly Brush VisibleStatusBrush = CreateFrozenBrush(87, 221, 179);
    private static readonly Brush MissingStatusBrush = CreateFrozenBrush(119, 139, 167);
    private static readonly Brush PendingStatusBrush = CreateFrozenBrush(255, 201, 85);
    private static readonly Brush MismatchedTeamStatusBrush = CreateFrozenBrush(255, 89, 116);
    private readonly IWindowCaptureSession _captureSession;
    private readonly IActiveMatchVisionDiagnosticsSource? _visionDiagnostics;
    private readonly LocalizationService _localization;
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
    private bool _isDisposed;

    public CaptureDiagnosticsViewModel(
        IWindowCaptureSession captureSession,
        bool isSupported,
        LocalizationService localization)
        : this(captureSession, isSupported, localization, visionDiagnostics: null)
    {
    }

    internal CaptureDiagnosticsViewModel(
        IWindowCaptureSession captureSession,
        bool isSupported,
        LocalizationService localization,
        IActiveMatchVisionDiagnosticsSource? visionDiagnostics)
    {
        _captureSession = captureSession ?? throw new ArgumentNullException(nameof(captureSession));
        _visionDiagnostics = visionDiagnostics;
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

    public IReadOnlyList<MinimapMarkerOverlayViewModel> MinimapMarkerOverlays =>
        _minimapMarkerOverlays;

    public bool HasParticipantDiagnostics => _visionSnapshot.Participants.Count > 0;

    public string ActivePlayerTeamText => string.Format(
        CultureInfo.CurrentCulture,
        _localization.Get("Settings.Capture.Participants.ActiveTeam.Value"),
        FormatRawTeam(_visionSnapshot.ActivePlayerTeam));

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

        var image = BitmapSource.Create(
            preview.ImageWidth,
            preview.ImageHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            preview.BgraPixels,
            preview.ImageStride);
        image.Freeze();
        _renderedPreviewAt = preview.CapturedAt;
        _minimapPreviewImage = image;
        _minimapMarkerOverlays = preview.ChampionMarkerCandidates
            .Select(candidate => new MinimapMarkerOverlayViewModel(
                candidate.X,
                candidate.Y,
                candidate.Width,
                candidate.Height,
                candidate.Affiliation == MinimapMarkerAffiliation.Ally
                    ? AllyMarkerBrush
                    : EnemyMarkerBrush,
                candidate.ChampionId,
                candidate.IdentityConfidence))
            .ToArray();
    }

    private void UpdateParticipantStatuses()
    {
        var recognized = _snapshot.MinimapPreview?.ChampionMarkerCandidates ??
            Array.Empty<MinimapChampionMarkerCandidate>();
        _allyParticipantStatuses = BuildParticipantStatuses(
            MinimapMarkerAffiliation.Ally,
            recognized);
        _enemyParticipantStatuses = BuildParticipantStatuses(
            MinimapMarkerAffiliation.Enemy,
            recognized);
    }

    private IReadOnlyList<VisionParticipantStatusViewModel> BuildParticipantStatuses(
        MinimapMarkerAffiliation affiliation,
        IReadOnlyList<MinimapChampionMarkerCandidate> recognized)
    {
        return _visionSnapshot.Participants
            .Where(participant => participant.RelativeAffiliation == affiliation)
            .OrderBy(static participant => GetPositionOrder(participant.Position))
            .ThenBy(static participant => participant.ChampionName, StringComparer.OrdinalIgnoreCase)
            .Select(participant => CreateParticipantStatus(participant, recognized))
            .ToArray();
    }

    private VisionParticipantStatusViewModel CreateParticipantStatus(
        ActiveMatchVisionParticipantSnapshot participant,
        IReadOnlyList<MinimapChampionMarkerCandidate> recognized)
    {
        var match = string.IsNullOrWhiteSpace(participant.ChampionId)
            ? null
            : recognized
                .Where(candidate =>
                    string.Equals(
                        candidate.ChampionId,
                        participant.ChampionId,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static candidate => candidate.IdentityConfidence)
                .FirstOrDefault();

        var relativeTeam = _localization.Get(
            participant.RelativeAffiliation == MinimapMarkerAffiliation.Ally
                ? "Settings.Capture.Participants.RelativeTeam.Ally"
                : "Settings.Capture.Participants.RelativeTeam.Enemy");
        var teamTrace = string.Format(
            CultureInfo.CurrentCulture,
            _localization.Get("Settings.Capture.Participants.TeamTrace.WithRing.Value"),
            FormatRawTeam(participant.RawTeam),
            relativeTeam,
            "—");

        string statusText;
        Brush statusBrush;
        if (string.IsNullOrWhiteSpace(participant.ChampionId))
        {
            statusText = _localization.Get(
                "Settings.Capture.Participants.Status.ReferencePending");
            statusBrush = PendingStatusBrush;
        }
        else if (match is not null)
        {
            var contourAffiliation = match.ContourAffiliation ?? match.Affiliation;
            var recognizedTeam = _localization.Get(
                contourAffiliation == MinimapMarkerAffiliation.Ally
                    ? "Settings.Capture.Participants.RelativeTeam.Ally"
                    : "Settings.Capture.Participants.RelativeTeam.Enemy");
            var isExpectedTeam = match.Affiliation == participant.RelativeAffiliation;
            statusText = string.Format(
                CultureInfo.CurrentCulture,
                _localization.Get(isExpectedTeam
                    ? "Settings.Capture.Participants.Status.Visible.Value"
                    : "Settings.Capture.Participants.Status.TeamMismatch.Value"),
                isExpectedTeam ? match.IdentityConfidence : recognizedTeam,
                match.IdentityConfidence);
            statusBrush = isExpectedTeam
                ? VisibleStatusBrush
                : MismatchedTeamStatusBrush;
            teamTrace = string.Format(
                CultureInfo.CurrentCulture,
                _localization.Get("Settings.Capture.Participants.TeamTrace.WithRing.Value"),
                FormatRawTeam(participant.RawTeam),
                relativeTeam,
                recognizedTeam);
        }
        else
        {
            statusText = _localization.Get(
                "Settings.Capture.Participants.Status.NotDetected");
            statusBrush = MissingStatusBrush;
        }

        return new VisionParticipantStatusViewModel(
            participant.ChampionName,
            FormatPositionCode(participant.Position),
            statusText,
            teamTrace,
            statusBrush);
    }

    private static string FormatRawTeam(TeamSide team) => team switch
    {
        TeamSide.Order => "ORDER",
        TeamSide.Chaos => "CHAOS",
        _ => "UNKNOWN"
    };

    private static string FormatPositionCode(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => "TOP",
        PlayerPosition.Jungle => "JGL",
        PlayerPosition.Middle => "MID",
        PlayerPosition.Bottom => "BOT",
        PlayerPosition.Support => "SUP",
        _ => "—"
    };

    private static int GetPositionOrder(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => 0,
        PlayerPosition.Jungle => 1,
        PlayerPosition.Middle => 2,
        PlayerPosition.Bottom => 3,
        PlayerPosition.Support => 4,
        _ => 5
    };

    private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
