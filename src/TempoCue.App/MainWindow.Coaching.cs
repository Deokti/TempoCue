using System.Windows;
using System.Windows.Controls;
using TempoCue.Core;

namespace TempoCue.App;

/// <summary>Ручной симулятор, показ советов и журнал срабатываний правил.</summary>
public partial class MainWindow
{
    private const int BlackCleaverItemId = 3071;
    private const string SimulatedPlayerId = "simulated-player";
    private const string SimulatedOpponentId = "simulated-opponent";

    private MatchState? _lastAdviceMatch;

    private void ShowInitialWaitingAdvice()
    {
        SetManualSimulationEnabled(isEnabled: true);
        AdviceTitleTextBlock.Text = _localization.Get("Advice.Waiting.Title");
        AdviceMessageTextBlock.Text = _localization.Get("Advice.Waiting.Message");
        AdviceSourceTextBlock.Text = _localization.Get("Advice.Waiting.Source");
        RuleIdTextBlock.Text = _localization.Get("Diagnostics.RuleEmpty");
        ApplyPowerState(_localization.Get("Power.Waiting"), "#243550", "#AFC0D4");
    }

    private void EvaluateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLiveMatchConnected && _latestLiveMatch is not null)
        {
            var liveAdvice = _coach.GetBestAdvice(_latestLiveMatch);
            ShowAdvice(liveAdvice);
            AppendLog(_localization.Get("Event.ManualRiotCheck"), _latestLiveMatch, liveAdvice);
            return;
        }

        EvaluateCurrentState(_localization.Get("Event.ManualCheck"));
    }

    private void EarlyLaneButton_Click(object sender, RoutedEventArgs e)
    {
        LevelSlider.Value = 1;
        BlackCleaverCheckBox.IsChecked = false;
        EvaluateCurrentState(_localization.Get("Event.PresetEarlyLane"));
    }

    private void PowerSpikeButton_Click(object sender, RoutedEventArgs e)
    {
        LevelSlider.Value = 9;
        BlackCleaverCheckBox.IsChecked = true;
        EvaluateCurrentState(_localization.Get("Event.PresetPowerSpike"));
    }

    private void CopyRawJsonButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(RawJsonTextBox.Text))
        {
            Clipboard.SetText(RawJsonTextBox.Text);
            LiveJsonInfoTextBlock.Text = _localization.Get("Diagnostics.JsonCopied");
        }
    }

    private void LevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LevelTextBlock is not null)
        {
            LevelTextBlock.Text = ((int)e.NewValue).ToString();
        }
    }

    private void EvaluateCurrentState(string eventName)
    {
        var itemIds = new HashSet<int>();
        if (BlackCleaverCheckBox.IsChecked == true)
        {
            itemIds.Add(BlackCleaverItemId);
        }

        var player = new PlayerState
        {
            ParticipantId = SimulatedPlayerId,
            ChampionName = GetSelectedChampion(PlayerChampionComboBox),
            Team = TeamSide.Order,
            Position = PlayerPosition.Top,
            Level = (int)LevelSlider.Value,
            ItemIds = itemIds
        };

        var opponent = new PlayerState
        {
            ParticipantId = SimulatedOpponentId,
            ChampionName = GetSelectedChampion(OpponentChampionComboBox),
            Team = TeamSide.Chaos,
            Position = PlayerPosition.Top,
            Level = (int)LevelSlider.Value
        };

        var match = new MatchState
        {
            GameTimeSeconds = 0,
            ActivePlayerId = SimulatedPlayerId,
            Players = [player, opponent]
        };

        var advice = _coach.GetBestAdvice(match);
        _lastAdviceMatch = match;
        ShowAdvice(advice);
        AppendLog(eventName, match, advice);
    }

    private void SetManualSimulationEnabled(bool isEnabled)
    {
        PlayerChampionComboBox.IsEnabled = isEnabled;
        OpponentChampionComboBox.IsEnabled = isEnabled;
        LevelSlider.IsEnabled = isEnabled;
        BlackCleaverCheckBox.IsEnabled = isEnabled;
        EarlyLaneButton.IsEnabled = isEnabled;
        PowerSpikeButton.IsEnabled = isEnabled;
        EvaluateButton.Content = isEnabled
            ? _localization.Get("Action.CheckSimulation")
            : _localization.Get("Action.CheckLiveMatch");
    }

    private void ShowAdvice(CoachAdvice? advice)
    {
        if (advice is null)
        {
            AdviceTitleTextBlock.Text = _localization.Get("Advice.NoRule.Title");
            AdviceMessageTextBlock.Text = _localization.Get("Advice.NoRule.Message");
            AdviceSourceTextBlock.Text = _localization.Get("Advice.NoRule.Source");
            RuleIdTextBlock.Text = _localization.Get("Diagnostics.RuleEmpty");
            ApplyPowerState(_localization.Get("Power.NoRule"), "#243550", "#AFC0D4");
            return;
        }

        AdviceTitleTextBlock.Text = advice.Title;
        AdviceMessageTextBlock.Text = advice.Message;
        AdviceSourceTextBlock.Text = advice.Source;
        RuleIdTextBlock.Text = _localization.Format(
            "Diagnostics.RuleLine",
            advice.RuleId,
            advice.Priority);

        var (labelKey, background, foreground) = advice.PowerState switch
        {
            PowerState.Dangerous => ("Power.Dangerous", "#3B2630", "#FF8E9D"),
            PowerState.Cautious => ("Power.Cautious", "#3B3421", "#F3C969"),
            PowerState.Playable => ("Power.Playable", "#19362F", "#76D7A8"),
            PowerState.Favorable => ("Power.Favorable", "#173548", "#76C9F0"),
            _ => throw new ArgumentOutOfRangeException(nameof(advice.PowerState))
        };

        ApplyPowerState(_localization.Get(labelKey), background, foreground);
    }

    private void ApplyPowerState(string label, string background, string foreground)
    {
        PowerStateTextBlock.Text = label;
        PowerBadgeBorder.Background = ToBrush(background);
        PowerStateTextBlock.Foreground = ToBrush(foreground);
    }

    private void AppendLog(string eventName, MatchState match, CoachAdvice? advice)
    {
        var matchup = MatchupResolver.Resolve(match);
        if (matchup is null)
        {
            EventLogListBox.Items.Insert(
                0,
                _localization.Format(
                    "Log.MatchupUnknown",
                    DateTime.Now.ToString("HH:mm:ss"),
                    eventName));
            return;
        }

        var items = matchup.Player.ItemIds.Count switch
        {
            0 => _localization.Get("Log.NoItems"),
            _ when matchup.Player.ItemIds.Contains(BlackCleaverItemId) => "Black Cleaver",
            _ => _localization.Plural("Count.Item", matchup.Player.ItemIds.Count)
        };

        var result = advice is null
            ? _localization.Get("Log.NoRule")
            : GetPowerStateLabel(advice.PowerState);
        EventLogListBox.Items.Insert(
            0,
            _localization.Format(
                "Log.Entry",
                DateTime.Now.ToString("HH:mm:ss"),
                eventName,
                matchup.Player.ChampionName,
                matchup.Opponent.ChampionName,
                matchup.Player.Level,
                items,
                result));
    }

    private string GetPowerStateLabel(PowerState powerState) => powerState switch
    {
        PowerState.Dangerous => _localization.Get("Power.Dangerous"),
        PowerState.Cautious => _localization.Get("Power.Cautious"),
        PowerState.Playable => _localization.Get("Power.Playable"),
        PowerState.Favorable => _localization.Get("Power.Favorable"),
        _ => throw new ArgumentOutOfRangeException(nameof(powerState), powerState, null)
    };

    private static string GetSelectedChampion(ComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? string.Empty
            : comboBox.SelectedItem?.ToString() ?? string.Empty;
    }

    private static void SetSelectedChampion(ComboBox comboBox, string championName)
    {
        var matchingItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Content?.ToString(),
                championName,
                StringComparison.OrdinalIgnoreCase));

        if (matchingItem is null)
        {
            matchingItem = new ComboBoxItem { Content = championName };
            comboBox.Items.Add(matchingItem);
        }

        comboBox.SelectedItem = matchingItem;
    }
}
