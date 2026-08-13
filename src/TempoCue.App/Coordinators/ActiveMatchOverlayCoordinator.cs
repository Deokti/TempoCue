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
            var activePlayer = snapshot.Match.GetActivePlayer();
            var levelIncreased = activePlayer is not null
                && _lastPlayerLevel is { } previousLevel
                && activePlayer.Level > previousLevel;
            if (activePlayer is not null)
            {
                _lastPlayerLevel = activePlayer.Level;
            }

            var matchupResult = ApplyMatchupSelection(update.CoachPackSelection, activePlayer);
            var mapReminderShown = ApplyMapControlPlan(
                update.MapControlPlan,
                previousGameTime,
                gameTimeSeconds);
            if (activePlayer is not null
                && levelIncreased
                && !matchupResult.NotificationShown
                && !mapReminderShown)
            {
                ApplyPersistentAdvice(
                    matchupResult.Context,
                    update.MapControlPlan,
                    activePlayer.Level);
            }
        }
    }

    private MatchupUpdateResult ApplyMatchupSelection(
        ActiveCoachPackSelection? selection,
        PlayerState? activePlayer)
    {
        if (selection?.State is not ActiveCoachPackSelectionState.Selected
            || selection.Pack is not { } pack
            || selection.Advice is not { } advice)
        {
            ResetMatchupSelectionState();
            return MatchupUpdateResult.Empty;
        }

        if (activePlayer is null)
        {
            ResetMatchupSelectionState();
            return MatchupUpdateResult.Empty;
        }

        var identity = new AdviceIdentity(pack.Id, advice.RuleId);
        var phaseChanged = _lastAdvice != identity;

        _lastAdvice = identity;

        if (phaseChanged)
        {
            Show(CreatePhaseNotification(pack, advice, activePlayer.Level));
            return new MatchupUpdateResult(
                new MatchupPersistentContext(pack, advice.RuleId),
                true);
        }

        return new MatchupUpdateResult(
            new MatchupPersistentContext(pack, advice.RuleId),
            false);
    }

    private bool ApplyMapControlPlan(
        ActiveMapControlPlan? plan,
        double? previousGameTimeSeconds,
        double gameTimeSeconds)
    {
        if (plan is null || plan.ScheduledAdvice.Count == 0)
        {
            return false;
        }

        var shown = false;
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
            shown = true;
        }

        return shown;
    }

    private void ApplyPersistentAdvice(
        MatchupPersistentContext? matchup,
        ActiveMapControlPlan? mapControlPlan,
        int playerLevel)
    {
        var candidates = new List<EligiblePersistentTip>();
        if (matchup is not null)
        {
            candidates.AddRange(matchup.Pack.PersistentTips
                .Where(tip => tip.ApplicableRuleId is null
                    || string.Equals(
                        tip.ApplicableRuleId,
                        matchup.ActiveRuleId,
                        StringComparison.Ordinal))
                .Select((tip, index) => new EligiblePersistentTip(
                    $"matchup:{matchup.Pack.Id}:persistent:{tip.Id}",
                    tip.ApplicableRuleId is not null ? 0 : 2,
                    index,
                    () => CreatePersistentTipNotification(matchup.Pack, tip, playerLevel))));
        }

        if (mapControlPlan is not null)
        {
            candidates.AddRange(mapControlPlan.AvailablePersistentAdvice
                .Select((tip, index) => new EligiblePersistentTip(
                    $"map-control:{tip.Id}",
                    1,
                    index,
                    () => CreateMapPersistentTipNotification(tip, playerLevel))));
        }

        var selected = SelectPersistentTip(candidates, playerLevel);
        if (selected is not null)
        {
            Show(selected.CreateNotification());
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
            _localization.Format("Current.Phase.Level", playerLevel),
            OverlayNotificationPurpose.Learning);

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

    private OverlayNotification CreateMapPersistentTipNotification(
        MapControlPersistentAdvice advice,
        int playerLevel) => new(
            $"map-control:{advice.Id}:level:{playerLevel}",
            _localization.Get("Overlay.MapControl.PersistentTip.Status"),
            advice.Title,
            advice.Message,
            _localization.Get("Overlay.MapControl.Dismiss"),
            OverlayNotificationTone.Coach,
            advice.PackName,
            _localization.Format("Current.Phase.Level", playerLevel),
            OverlayNotificationPurpose.Learning);

    private EligiblePersistentTip? SelectPersistentTip(
        IReadOnlyCollection<EligiblePersistentTip> eligibleTips,
        int playerLevel)
    {
        var frequency = _overlaySettings.Current.PersistentAdviceFrequency;
        if (frequency is PersistentAdviceFrequency.Disabled || eligibleTips.Count == 0)
        {
            return null;
        }

        var orderedTips = eligibleTips
            .OrderBy(candidate => candidate.ScopeRank)
            .ThenBy(candidate => candidate.SourceIndex)
            .ToArray();
        var selected = orderedTips.FirstOrDefault(candidate =>
            !_persistentTipLevels.ContainsKey(candidate.Id));
        if (frequency is PersistentAdviceFrequency.EveryLevel)
        {
            selected ??= orderedTips
                .Where(candidate =>
                    playerLevel - _persistentTipLevels[candidate.Id]
                    >= PersistentTipRepeatLevelGap)
                .OrderBy(candidate => _persistentTipLevels[candidate.Id])
                .ThenBy(candidate => candidate.ScopeRank)
                .ThenBy(candidate => candidate.SourceIndex)
                .FirstOrDefault();
        }
        if (selected is null)
        {
            return null;
        }

        _persistentTipLevels[selected.Id] = playerLevel;
        return selected;
    }

    private void ResetMatchState()
    {
        ResetMatchupSelectionState();
        _shownMapAdvice.Clear();
        _persistentTipLevels.Clear();
        _lastGameTimeSeconds = null;
        _lastPlayerLevel = null;
    }

    private void ResetMatchupSelectionState()
    {
        _lastAdvice = null;
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

    private sealed record MatchupPersistentContext(
        CoachPack Pack,
        string ActiveRuleId);

    private sealed record MatchupUpdateResult(
        MatchupPersistentContext? Context,
        bool NotificationShown)
    {
        public static MatchupUpdateResult Empty { get; } = new(null, false);
    }

    private sealed record EligiblePersistentTip(
        string Id,
        int ScopeRank,
        int SourceIndex,
        Func<OverlayNotification> CreateNotification);
}
