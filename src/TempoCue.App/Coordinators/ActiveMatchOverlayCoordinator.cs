using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Overlay;

namespace TempoCue.App.Coordinators;

/// <summary>
/// Передаёт в игровой оверлей уже выбранный движком совет при смене фазы матчапа.
/// Координатор не вычисляет правила повторно и не зависит от Riot DTO.
/// </summary>
public sealed class ActiveMatchOverlayCoordinator : IDisposable
{
    private const double MatchRestartToleranceSeconds = 5;
    private const int PersistentTipRepeatLevelGap = 3;

    private readonly IActiveMatchMonitor _monitor;
    private readonly IOverlayPresenter _presenter;
    private readonly LocalizationService _localization;
    private readonly OverlaySettingsState _overlaySettings;
    private readonly object _stateLock = new();
    private readonly Dictionary<string, int> _persistentTipLevels =
        new(StringComparer.Ordinal);

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
            if (_lastGameTimeSeconds is { } previousGameTime
                && gameTimeSeconds + MatchRestartToleranceSeconds < previousGameTime)
            {
                ResetSelectionState();
            }

            _lastGameTimeSeconds = gameTimeSeconds;

            var selection = update.CoachPackSelection;
            if (selection?.State is not ActiveCoachPackSelectionState.Selected
                || selection.Pack is not { } pack
                || selection.Advice is not { } advice)
            {
                ResetSelectionState();
                return;
            }

            var activePlayer = snapshot.Match.GetActivePlayer();
            if (activePlayer is null)
            {
                ResetSelectionState();
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
    }

    private void Show(OverlayNotification notification)
    {
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
        ResetSelectionState();
        _lastGameTimeSeconds = null;
    }

    private void ResetSelectionState()
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

    private sealed record AdviceIdentity(string PackId, string RuleId);

    private sealed record EligiblePersistentTip(
        CoachPersistentTipDefinition Tip,
        int SourceIndex,
        bool IsPhaseSpecific);
}
