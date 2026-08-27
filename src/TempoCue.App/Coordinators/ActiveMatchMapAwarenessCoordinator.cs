using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;
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

        // Правила берутся из плана включённых наборов: приложение не знает
        // наперёд, о каких ролях и событиях уведомлять. ADR 0016.
        var rules = _monitor.CurrentUpdate?.MapControlPlan?.AvailableVisionRules ?? [];
        var signal = MapAwarenessSignalPolicy.Evaluate(transition, snapshot.Match, rules);
        if (signal is null)
        {
            return;
        }

        _presenter.ApplySettings(_overlaySettings.Current.ToPresentationSettings());
        _presenter.SetInteractionMode(OverlayInteractionMode.Game);
        _presenter.Show(CreateNotification(signal));
    }

    /// <summary>
    /// Уведомление по сработавшему правилу. Текст принадлежит набору, а не
    /// приложению: локализованными остаются только оформление окна и название
    /// роли — то, что относится к самому TempoCue, а не к содержанию совета.
    /// </summary>
    private OverlayNotification CreateNotification(MapAwarenessSignal signal)
    {
        var rule = signal.Rule;
        return new OverlayNotification(
            $"map-awareness:{rule.Id}:{signal.ParticipantId}",
            _localization.Get("Overlay.MapAwareness.Status"),
            signal.ChampionName,
            rule.Message,
            _localization.Get("Overlay.MapAwareness.Dismiss"),
            ToTone(rule.Priority),
            badge: _localization.Get(ToPositionKey(rule.Position)));
    }

    private static OverlayNotificationTone ToTone(MapObjectiveReminderPriority priority) =>
        priority switch
        {
            MapObjectiveReminderPriority.Important => OverlayNotificationTone.Danger,
            MapObjectiveReminderPriority.Caution => OverlayNotificationTone.Caution,
            _ => OverlayNotificationTone.Coach
        };

    private static string ToPositionKey(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => "Position.Top",
        PlayerPosition.Jungle => "Position.Jungle",
        PlayerPosition.Middle => "Position.Middle",
        PlayerPosition.Bottom => "Position.Bottom",
        PlayerPosition.Support => "Position.Support",
        _ => "Position.Unknown"
    };
}
