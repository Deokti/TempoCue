using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;
using TempoCue.App.Settings;
using TempoCue.App.ViewModels;
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
    /// Уведомление по сработавшему правилу. Собирается той же фабрикой, что и
    /// предпросмотр в редакторе: автор обязан видеть ровно то, что увидит игрок.
    /// </summary>
    private OverlayNotification CreateNotification(MapAwarenessSignal signal) =>
        MapVisionNotificationFactory.Create(
            signal.Rule,
            signal.ChampionName,
            _localization,
            signal.ParticipantId);
}
