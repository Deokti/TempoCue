using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.Application;
using TempoCue.Coaching;
using TempoCue.Core;
using TempoCue.Overlay;

namespace TempoCue.App.Coordinators;

/// <summary>
/// Передаёт в игровой оверлей готовые советы матчапа и запланированные напоминания карты.
/// Координатор не вычисляет правила повторно и не зависит от Riot DTO.
/// </summary>
public sealed class ActiveMatchOverlayCoordinator : IDisposable
{
    private const double MatchRestartToleranceSeconds = 5;
    private const double InitialMapReminderCatchUpSeconds = 2;
    private const int PersistentTipRepeatLevelGap = 3;

    private readonly IActiveMatchMonitor _monitor;
    private readonly IOverlayPresenter _presenter;
    private readonly LocalizationService _localization;
    private readonly OverlaySettingsState _overlaySettings;
    private readonly object _stateLock = new();
    private readonly Dictionary<string, int> _persistentTipLevels =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _shownMapAdvice = new(StringComparer.Ordinal);

    private AdviceIdentity? _lastAdvice;
    private double? _lastGameTimeSeconds;
    private string? _activePackId;
    private int? _lastPlayerLevel;
    private bool _isDisposed;

    public ActiveMatchOverlayCoordinator(
        IActiveMatchMonitor monitor,
        IOverlayPresenter presenter,
        LocalizationService localization,
        OverlaySettingsState? overlaySettings = null)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _overlaySettings = overlaySettings ?? new OverlaySettingsState(new OverlayUserSettings
        {
            PersistentAdviceFrequency = PersistentAdviceFrequency.EveryLevel
        });

        _monitor.Updated += Monitor_Updated;
        if (_monitor.CurrentUpdate is { } currentUpdate)
        {
            ApplyUpdate(currentUpdate);
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _monitor.Updated -= Monitor_Updated;
        }
    }

    private void Monitor_Updated(object? sender, ActiveMatchUpdateEventArgs e) =>
        ApplyUpdate(e.Update);

    private void ApplyUpdate(ActiveMatchUpdate update)
    {
        lock (_stateLock)
        {
            if (_isDisposed)
            {
                return;
            }

            if (update.Status is MatchSnapshotReadStatus.NoActiveMatch
                or MatchSnapshotReadStatus.GameLoading)
            {
                ResetMatchState();
                return;
            }

            if (update.Status is not MatchSnapshotReadStatus.Success
                || update.Snapshot is not { } snapshot)
            {
                return;
            }

            var gameTimeSeconds = snapshot.Match.GameTimeSeconds;
            var previousGameTime = _lastGameTimeSeconds;
            if (previousGameTime is { } previousTime
                && gameTimeSeconds + MatchRestartToleranceSeconds < previousTime)
            {
                ResetMatchState();
                previousGameTime = null;
            }

            _lastGameTimeSeconds = gameTimeSeconds;
            ApplyMatchupSelection(update.CoachPackSelection, snapshot);
            ApplyMapControlPlan(
                update.MapControlPlan,
                previousGameTime,
                gameTimeSeconds);
        }
    }

    private void ApplyMatchupSelection(
        ActiveCoachPackSelection? selection,
        MatchSnapshot snapshot)
    {
        if (selection?.State is not ActiveCoachPackSelectionState.Selected
            || selection.Pack is not { } pack
            || selection.Advice is not { } advice)
        {
            ResetMatchupSelectionState();
            return;
        }

        var activePlayer = snapshot.Match.GetActivePlayer();
        if (activePlayer is null)
        {
            ResetMatchupSelectionState();
            return;
        }

        var packChanged = !string.Equals(
            _activePackId,
            pack.Id,
            StringComparison.Ordinal);
        if (packChanged)
        {
            _persistentTipLevels.Clear();
        }

        var identity = new AdviceIdentity(pack.Id, advice.RuleId);
        var phaseChanged = _lastAdvice != identity;
        var levelIncreased = _lastPlayerLevel is { } previousLevel
            && activePlayer.Level > previousLevel;

        _activePackId = pack.Id;
        _lastAdvice = identity;
        _lastPlayerLevel = activePlayer.Level;

        if (phaseChanged)
        {
            Show(CreatePhaseNotification(pack, advice, activePlayer.Level));
            return;
        }

        if (!levelIncreased)
        {
            return;
        }

        var persistentTip = SelectPersistentTip(
            pack,
            advice.RuleId,
            activePlayer.Level);
        if (persistentTip is not null)
        {
            Show(CreatePersistentTipNotification(pack, persistentTip, activePlayer.Level));
        }
    }

    private void ApplyMapControlPlan(
        ActiveMapControlPlan? plan,
        double? previousGameTimeSeconds,
        double gameTimeSeconds)
    {
        if (plan is null || plan.ScheduledAdvice.Count == 0)
        {
            return;
        }

        var catchUpBound = Math.Max(0, gameTimeSeconds - InitialMapReminderCatchUpSeconds);
        var lowerBound = previousGameTimeSeconds is { } previousTime
            ? Math.Min(previousTime, catchUpBound)
            : catchUpBound;
        foreach (var advice in plan.ScheduledAdvice.Where(advice =>
                     advice.TriggerTime.TotalSeconds > lowerBound
                     && advice.TriggerTime.TotalSeconds <= gameTimeSeconds
                     && !_shownMapAdvice.Contains(advice.Id)))
        {
            _shownMapAdvice.Add(advice.Id);
            Show(CreateMapControlNotification(advice));
        }
    }

    private void Show(OverlayNotification notification)
    {
        // Игровое событие не должно наследовать несохранённый черновик предпросмотра.
        _presenter.ApplySettings(_overlaySettings.Current.ToPresentationSettings());
        _presenter.SetInteractionMode(OverlayInteractionMode.Game);
        _presenter.Show(notification);
    }

    private OverlayNotification CreatePhaseNotification(
        CoachPack pack,
        CoachAdvice advice,
        int playerLevel) => new(
            $"matchup:{pack.Id}:{advice.RuleId}",
            _localization.Get(GetPowerStateResourceKey(advice.PowerState)),
            advice.Title,
            advice.Message,
            _localization.Get("Overlay.Matchup.Dismiss"),
            GetTone(advice.PowerState),
            pack.Name,
            _localization.Format("Current.Phase.Level", playerLevel));

    private OverlayNotification CreatePersistentTipNotification(
        CoachPack pack,
        CoachPersistentTipDefinition tip,
        int playerLevel) => new(
            $"matchup:{pack.Id}:tip:{tip.Id}:level:{playerLevel}",
            _localization.Get("Overlay.Matchup.PersistentTip.Status"),
            tip.Title,
            tip.Message,
            _localization.Get("Overlay.Matchup.Dismiss"),
            OverlayNotificationTone.Coach,
            pack.Name,
            _localization.Format("Current.Phase.Level", playerLevel));

    private OverlayNotification CreateMapControlNotification(
        MapControlScheduledAdvice advice) => new(
            $"map-control:{advice.Id}",
            _localization.Get("Overlay.MapControl.Status"),
            string.IsNullOrWhiteSpace(advice.Title)
                ? GetObjectiveName(advice.Objective)
                : advice.Title,
            advice.Message,
            _localization.Get("Overlay.MapControl.Dismiss"),
            GetTone(advice.Priority),
            advice.PackName,
            _localization.Format(
                "Overlay.MapControl.TargetTime",
                FormatTime(advice.TargetTime)));

    private CoachPersistentTipDefinition? SelectPersistentTip(
        CoachPack pack,
        string activeRuleId,
        int playerLevel)
    {
        var frequency = _overlaySettings.Current.PersistentAdviceFrequency;
        if (frequency is PersistentAdviceFrequency.Disabled)
        {
            return null;
        }

        var eligibleTips = pack.PersistentTips
            .Where(tip => tip.ApplicableRuleId is null
                || string.Equals(
                    tip.ApplicableRuleId,
                    activeRuleId,
                    StringComparison.Ordinal))
            .Select((tip, index) => new EligiblePersistentTip(
                tip,
                index,
                tip.ApplicableRuleId is not null))
            .OrderByDescending(candidate => candidate.IsPhaseSpecific)
            .ThenBy(candidate => candidate.SourceIndex)
            .ToArray();
        if (eligibleTips.Length == 0)
        {
            return null;
        }

        var selected = eligibleTips.FirstOrDefault(candidate =>
            !_persistentTipLevels.ContainsKey(candidate.Tip.Id));
        if (frequency is PersistentAdviceFrequency.EveryLevel)
        {
            selected ??= eligibleTips
                .Where(candidate =>
                    playerLevel - _persistentTipLevels[candidate.Tip.Id]
                    >= PersistentTipRepeatLevelGap)
                .OrderBy(candidate => _persistentTipLevels[candidate.Tip.Id])
                .ThenByDescending(candidate => candidate.IsPhaseSpecific)
                .ThenBy(candidate => candidate.SourceIndex)
                .FirstOrDefault();
        }
        if (selected is null)
        {
            return null;
        }

        _persistentTipLevels[selected.Tip.Id] = playerLevel;
        return selected.Tip;
    }

    private void ResetMatchState()
    {
        ResetMatchupSelectionState();
        _shownMapAdvice.Clear();
        _lastGameTimeSeconds = null;
    }

    private void ResetMatchupSelectionState()
    {
        _lastAdvice = null;
        _activePackId = null;
        _lastPlayerLevel = null;
        _persistentTipLevels.Clear();
    }

    private static string GetPowerStateResourceKey(PowerState powerState) => powerState switch
    {
        PowerState.Dangerous => "Power.Dangerous",
        PowerState.Cautious => "Power.Cautious",
        PowerState.Playable => "Power.Playable",
        PowerState.Favorable => "Power.Favorable",
        _ => throw new ArgumentOutOfRangeException(nameof(powerState), powerState, null)
    };

    private static OverlayNotificationTone GetTone(PowerState powerState) => powerState switch
    {
        PowerState.Dangerous => OverlayNotificationTone.Danger,
        PowerState.Cautious => OverlayNotificationTone.Caution,
        PowerState.Playable or PowerState.Favorable => OverlayNotificationTone.Coach,
        _ => throw new ArgumentOutOfRangeException(nameof(powerState), powerState, null)
    };

    private static OverlayNotificationTone GetTone(
        MapObjectiveReminderPriority priority) => priority switch
    {
        MapObjectiveReminderPriority.Important => OverlayNotificationTone.Danger,
        MapObjectiveReminderPriority.Caution => OverlayNotificationTone.Caution,
        MapObjectiveReminderPriority.Information => OverlayNotificationTone.Information,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null)
    };

    private string GetObjectiveName(MapObjectiveKind? objective) => objective switch
    {
        MapObjectiveKind.ElementalDragon => _localization.Get(
            "Coaching.MapControlEditor.Content.Objective.Dragon"),
        MapObjectiveKind.VoidGrubs => _localization.Get(
            "Coaching.MapControlEditor.Content.Objective.Grubs"),
        MapObjectiveKind.RiftHerald => _localization.Get(
            "Coaching.MapControlEditor.Content.Objective.Herald"),
        MapObjectiveKind.BaronNashor => _localization.Get(
            "Coaching.MapControlEditor.Content.Objective.Baron"),
        _ => _localization.Get("Overlay.MapControl.GenericTitle")
    };

    private static string FormatTime(TimeSpan value) =>
        $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";

    private sealed record AdviceIdentity(string PackId, string RuleId);

    private sealed record EligiblePersistentTip(
        CoachPersistentTipDefinition Tip,
        int SourceIndex,
        bool IsPhaseSpecific);
}
