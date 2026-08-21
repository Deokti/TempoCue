using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.Application;
using TempoCue.Overlay;

namespace TempoCue.App.Coordinators;

/// <summary>Показывает в игровом оверлее подтверждённые сигналы видимости противников.</summary>
public sealed class ActiveMatchMapAwarenessCoordinator : IDisposable
{
    private readonly IActiveMatchMonitor _monitor;
    private readonly IParticipantVisibilityTransitionSource _transitionSource;
    private readonly IOverlayPresenter _presenter;
    private readonly LocalizationService _localization;
    private readonly OverlaySettingsState _overlaySettings;
    private bool _isDisposed;

    public ActiveMatchMapAwarenessCoordinator(
        IActiveMatchMonitor monitor,
        IParticipantVisibilityTransitionSource transitionSource,
        IOverlayPresenter presenter,
        LocalizationService localization,
        OverlaySettingsState overlaySettings)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _transitionSource = transitionSource
            ?? throw new ArgumentNullException(nameof(transitionSource));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _overlaySettings = overlaySettings ?? throw new ArgumentNullException(nameof(overlaySettings));

        _transitionSource.TransitionAvailable += TransitionSource_TransitionAvailable;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _transitionSource.TransitionAvailable -= TransitionSource_TransitionAvailable;
    }

    private void TransitionSource_TransitionAvailable(
        object? sender,
        ParticipantVisibilityTransition transition)
    {
        if (_isDisposed
            || _monitor.CurrentUpdate is not
            {
                Status: MatchSnapshotReadStatus.Success,
                Snapshot: { } snapshot
            })
        {
            return;
        }

        var signal = MapAwarenessSignalPolicy.Evaluate(transition, snapshot.Match);
        if (signal is null)
        {
            return;
        }

        _presenter.ApplySettings(_overlaySettings.Current.ToPresentationSettings());
        _presenter.SetInteractionMode(OverlayInteractionMode.Game);
        _presenter.Show(CreateNotification(signal));
    }

    private OverlayNotification CreateNotification(MapAwarenessSignal signal) => signal.Kind switch
    {
        MapAwarenessSignalKind.EnemyJunglerAppeared => new OverlayNotification(
            $"map-awareness:jungle:{signal.ParticipantId}:appeared",
            _localization.Get("Overlay.MapAwareness.Status"),
            _localization.Get("Overlay.MapAwareness.JunglerAppeared.Title"),
            _localization.Format(
                "Overlay.MapAwareness.JunglerAppeared.Message",
                signal.ChampionName),
            _localization.Get("Overlay.MapAwareness.Dismiss"),
            OverlayNotificationTone.Danger,
            badge: _localization.Get("Position.Jungle")),
        MapAwarenessSignalKind.EnemyMidDisappeared => new OverlayNotification(
            $"map-awareness:middle:{signal.ParticipantId}:disappeared",
            _localization.Get("Overlay.MapAwareness.Status"),
            _localization.Get("Overlay.MapAwareness.MidDisappeared.Title"),
            _localization.Format(
                "Overlay.MapAwareness.MidDisappeared.Message",
                signal.ChampionName),
            _localization.Get("Overlay.MapAwareness.Dismiss"),
            OverlayNotificationTone.Caution,
            badge: _localization.Get("Position.Middle")),
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal.Kind, null)
    };
}
