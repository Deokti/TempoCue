using System.Windows;
using System.Windows.Threading;

namespace TempoCue.Overlay;

/// <summary>Владеет окном, планировщиком карточек и одним UI-таймером жизненного цикла.</summary>
public sealed class OverlayHost : IOverlayPresenter
{
    private const double WorkAreaRightMargin = 24;
    private const double WorkAreaTopMargin = 80;
    private static readonly TimeSpan LifecycleTick = TimeSpan.FromMilliseconds(100);

    private readonly Dispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly OverlayDisplayPolicy _policy;
    private readonly OverlayNotificationScheduler _scheduler;
    private readonly DispatcherTimer _lifecycleTimer;

    private OverlayWindow? _window;
    private OverlayScheduleSnapshot _lastSnapshot = OverlayScheduleSnapshot.Empty;
    private OverlayPresentationSettings _settings;
    private bool _isApplyingPosition;
    private bool _isDisposed;

    public OverlayHost(
        Dispatcher dispatcher,
        TimeProvider? timeProvider = null,
        OverlayDisplayPolicy? policy = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _policy = policy ?? new OverlayDisplayPolicy();
        _settings = _policy.PresentationSettings.Normalize();
        _policy.ApplySettings(_settings);
        _scheduler = new OverlayNotificationScheduler(_policy);
        _lifecycleTimer = new DispatcherTimer(
            LifecycleTick,
            DispatcherPriority.Background,
            LifecycleTimer_Tick,
            _dispatcher);
        _lifecycleTimer.Stop();
    }

    public OverlayInteractionMode InteractionMode { get; private set; } =
        OverlayInteractionMode.Game;

    /// <summary>Сообщает владельцу приложения, что пользователь закрыл режим настройки.</summary>
    public event EventHandler? SetupDismissed;

    public OverlayPresentationSettings Settings => _settings;

    internal Window? CurrentWindow => _window;

    internal OverlayScheduleSnapshot CurrentSnapshot => _scheduler.Current;

    public void ApplySettings(OverlayPresentationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => ApplySettings(settings));
            return;
        }

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _settings = settings.Normalize();
        _policy.ApplySettings(_settings);
        if (_window is not null)
        {
            _window.ApplySettings(_settings);
            PositionInWorkArea(_window);
        }

        if (!_settings.IsEnabled && InteractionMode is OverlayInteractionMode.Game)
        {
            Hide();
        }
    }

    public void SetInteractionMode(OverlayInteractionMode mode)
    {
        if (!_dispatcher.CheckAccess())
        {
            _ = _dispatcher.InvokeAsync(() => SetInteractionMode(mode));
            return;
        }

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        InteractionMode = mode;
        _window?.SetInteractionMode(mode);
    }

    public void Show(OverlayNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (!_dispatcher.CheckAccess())
        {
            _ = _dispatcher.InvokeAsync(() => Show(notification));
            return;
        }

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!_settings.IsEnabled && InteractionMode is OverlayInteractionMode.Game)
        {
            return;
        }

        var snapshot = _scheduler.Publish(notification, _timeProvider.GetUtcNow());
        ApplySnapshot(snapshot);
    }

    public void ShowPreview(IReadOnlyList<OverlayNotification> notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (notifications.Count == 0)
        {
            throw new ArgumentException("Предпросмотр должен содержать хотя бы одно уведомление.", nameof(notifications));
        }

        var previewNotifications = notifications.ToArray();
        if (previewNotifications.Any(static notification => notification is null))
        {
            throw new ArgumentException("Предпросмотр не может содержать пустое уведомление.", nameof(notifications));
        }

        if (!_dispatcher.CheckAccess())
        {
            _ = _dispatcher.InvokeAsync(() => ShowPreview(previewNotifications));
            return;
        }

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _lastSnapshot = _scheduler.Reset();
        var now = _timeProvider.GetUtcNow();
        var snapshot = _scheduler.Current;
        foreach (var notification in previewNotifications)
        {
            snapshot = _scheduler.Publish(notification, now);
        }

        ApplySnapshot(snapshot);
    }

    public void Hide()
    {
        if (!_dispatcher.CheckAccess())
        {
            _ = _dispatcher.InvokeAsync(Hide);
            return;
        }

        if (_isDisposed)
        {
            return;
        }

        _lifecycleTimer.Stop();
        _lastSnapshot = _scheduler.Clear();
        _window?.HideNotifications();
    }

    public void Dispose()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Dispose);
            return;
        }

        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifecycleTimer.Stop();
        _lifecycleTimer.Tick -= LifecycleTimer_Tick;
        if (_window is null)
        {
            return;
        }

        _window.HideRequested -= Window_HideRequested;
        _window.AllCardsHidden -= Window_AllCardsHidden;
        _window.LocationChanged -= Window_LocationChanged;
        _window.Close();
        _window = null;
    }

    private OverlayWindow CreateWindow()
    {
        var window = new OverlayWindow(_policy);
        window.HideRequested += Window_HideRequested;
        window.AllCardsHidden += Window_AllCardsHidden;
        window.LocationChanged += Window_LocationChanged;
        window.SetInteractionMode(InteractionMode);
        window.ApplySettings(_settings);
        return window;
    }

    private void ApplySnapshot(OverlayScheduleSnapshot snapshot)
    {
        if (SnapshotsEqual(_lastSnapshot, snapshot))
        {
            return;
        }

        _lastSnapshot = snapshot;
        if (snapshot.Items.Count == 0)
        {
            _window?.HideNotifications();
            return;
        }

        var isNewWindow = _window is null;
        _window ??= CreateWindow();
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.ShowNotifications(snapshot);
        _lifecycleTimer.Start();
        if (isNewWindow)
        {
            PositionInWorkArea(_window);
        }
    }

    private static bool SnapshotsEqual(
        OverlayScheduleSnapshot left,
        OverlayScheduleSnapshot right)
    {
        return left.HasPending == right.HasPending &&
               left.Items.Count == right.Items.Count &&
               left.Items.Zip(right.Items).All(pair => pair.First == pair.Second);
    }

    private void PositionInWorkArea(Window window)
    {
        window.UpdateLayout();
        var workArea = SystemParameters.WorkArea;
        var availableWidth = Math.Max(0, workArea.Width - window.ActualWidth);
        var availableHeight = Math.Max(0, workArea.Height - window.ActualHeight);
        _isApplyingPosition = true;
        try
        {
            window.Left = _settings.HorizontalPosition is { } horizontal
                ? workArea.Left + availableWidth * horizontal
                : Math.Max(workArea.Left, workArea.Right - window.ActualWidth - WorkAreaRightMargin);
            window.Top = _settings.VerticalPosition is { } vertical
                ? workArea.Top + availableHeight * vertical
                : workArea.Top + WorkAreaTopMargin;
        }
        finally
        {
            _isApplyingPosition = false;
        }
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_isApplyingPosition || InteractionMode is not OverlayInteractionMode.Setup || sender is not Window window)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var availableWidth = Math.Max(1, workArea.Width - window.ActualWidth);
        var availableHeight = Math.Max(1, workArea.Height - window.ActualHeight);
        _settings = _settings with
        {
            HorizontalPosition = Math.Clamp((window.Left - workArea.Left) / availableWidth, 0, 1),
            VerticalPosition = Math.Clamp((window.Top - workArea.Top) / availableHeight, 0, 1)
        };
        _policy.ApplySettings(_settings);
    }

    private void LifecycleTimer_Tick(object? sender, EventArgs e)
    {
        var snapshot = _scheduler.Advance(_timeProvider.GetUtcNow());
        ApplySnapshot(snapshot);
        if (snapshot.Items.Count == 0 && !snapshot.HasPending)
        {
            _lifecycleTimer.Stop();
        }
    }

    private void Window_HideRequested(object? sender, EventArgs e)
    {
        var dismissedSetup = InteractionMode is OverlayInteractionMode.Setup;
        Hide();
        if (!dismissedSetup)
        {
            return;
        }

        SetInteractionMode(OverlayInteractionMode.Game);
        SetupDismissed?.Invoke(this, EventArgs.Empty);
    }

    private void Window_AllCardsHidden(object? sender, EventArgs e)
    {
        if (_scheduler.Current.Items.Count == 0)
        {
            _window?.Hide();
        }
    }
}
