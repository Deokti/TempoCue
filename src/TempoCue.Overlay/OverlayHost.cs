using System.Windows;
using System.Windows.Threading;

namespace TempoCue.Overlay;

/// <summary>Владеет окном, планировщиком карточек и одним UI-таймером жизненного цикла.</summary>
public sealed class OverlayHost : IOverlayPresenter
{
    private const double WorkAreaMargin = 24;
    private static readonly TimeSpan LifecycleTick = TimeSpan.FromMilliseconds(100);

    private readonly Dispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly OverlayDisplayPolicy _policy;
    private readonly OverlayNotificationScheduler _scheduler;
    private readonly DispatcherTimer _lifecycleTimer;

    private OverlayWindow? _window;
    private OverlayScheduleSnapshot _lastSnapshot = OverlayScheduleSnapshot.Empty;
    private bool _isDisposed;

    public OverlayHost(
        Dispatcher dispatcher,
        TimeProvider? timeProvider = null,
        OverlayDisplayPolicy? policy = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _policy = policy ?? new OverlayDisplayPolicy();
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

    internal Window? CurrentWindow => _window;

    internal OverlayScheduleSnapshot CurrentSnapshot => _scheduler.Current;

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
        var snapshot = _scheduler.Publish(notification, _timeProvider.GetUtcNow());
        ApplySnapshot(snapshot);
    }

    public void ShowPreview(OverlayNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (!_dispatcher.CheckAccess())
        {
            _ = _dispatcher.InvokeAsync(() => ShowPreview(notification));
            return;
        }

        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _lastSnapshot = _scheduler.Reset();
        ApplySnapshot(_scheduler.Publish(notification, _timeProvider.GetUtcNow()));
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
        _window.Close();
        _window = null;
    }

    private OverlayWindow CreateWindow()
    {
        var window = new OverlayWindow(_policy);
        window.HideRequested += Window_HideRequested;
        window.AllCardsHidden += Window_AllCardsHidden;
        window.SetInteractionMode(InteractionMode);
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

    private static void PositionInWorkArea(Window window)
    {
        window.UpdateLayout();
        var workArea = SystemParameters.WorkArea;
        window.Left = Math.Max(workArea.Left, workArea.Right - window.ActualWidth - WorkAreaMargin);
        window.Top = workArea.Top + WorkAreaMargin;
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

    private void Window_HideRequested(object? sender, EventArgs e) => Hide();

    private void Window_AllCardsHidden(object? sender, EventArgs e)
    {
        if (_scheduler.Current.Items.Count == 0)
        {
            _window?.Hide();
        }
    }
}
