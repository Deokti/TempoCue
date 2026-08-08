using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RiftCoach.Core;

namespace RiftCoach.App;

public partial class MainWindow : Window
{
    private const int BlackCleaverItemId = 3071;

    private readonly MatchupCoach _coach;

    public MainWindow()
    {
        InitializeComponent();

        var packPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "CoachPacks",
            "urgot-vs-sett.json");

        var pack = CoachPackLoader.LoadFromFile(packPath);
        _coach = new MatchupCoach(pack.Rules);

        Loaded += (_, _) => EvaluateCurrentState("Стартовый сценарий");
    }

    private void EvaluateButton_Click(object sender, RoutedEventArgs e)
    {
        EvaluateCurrentState("Ручная проверка");
    }

    private void EarlyLaneButton_Click(object sender, RoutedEventArgs e)
    {
        LevelSlider.Value = 1;
        BlackCleaverCheckBox.IsChecked = false;
        EvaluateCurrentState("Пресет: начало линии");
    }

    private void PowerSpikeButton_Click(object sender, RoutedEventArgs e)
    {
        LevelSlider.Value = 9;
        BlackCleaverCheckBox.IsChecked = true;
        EvaluateCurrentState("Пресет: power spike");
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

        var state = new GameState
        {
            PlayerChampion = GetSelectedChampion(PlayerChampionComboBox),
            OpponentChampion = GetSelectedChampion(OpponentChampionComboBox),
            PlayerLevel = (int)LevelSlider.Value,
            PlayerItemIds = itemIds
        };

        var advice = _coach.GetBestAdvice(state);
        ShowAdvice(advice);
        AppendLog(eventName, state, advice);
    }

    private void ShowAdvice(CoachAdvice? advice)
    {
        if (advice is null)
        {
            AdviceTitleTextBlock.Text = "Подходящее правило не найдено";
            AdviceMessageTextBlock.Text = "В загруженном Coach Pack пока нет совета для выбранного состояния.";
            AdviceSourceTextBlock.Text = "Добавление новых матчапов станет отдельным этапом.";
            RuleIdTextBlock.Text = "RULE: —";
            ApplyPowerState("НЕТ ПРАВИЛА", "#243550", "#AFC0D4");
            return;
        }

        AdviceTitleTextBlock.Text = advice.Title;
        AdviceMessageTextBlock.Text = advice.Message;
        AdviceSourceTextBlock.Text = advice.Source;
        RuleIdTextBlock.Text = $"RULE: {advice.RuleId} · PRIORITY: {advice.Priority}";

        switch (advice.PowerState)
        {
            case PowerState.Dangerous:
                ApplyPowerState("ОПАСНО", "#3B2630", "#FF8E9D");
                break;
            case PowerState.Cautious:
                ApplyPowerState("ОСТОРОЖНО", "#3B3421", "#F3C969");
                break;
            case PowerState.Playable:
                ApplyPowerState("МОЖНО ИГРАТЬ", "#19362F", "#76D7A8");
                break;
            case PowerState.Favorable:
                ApplyPowerState("ПРЕИМУЩЕСТВО", "#173548", "#76C9F0");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ApplyPowerState(string label, string background, string foreground)
    {
        PowerStateTextBlock.Text = label;
        PowerBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
        PowerStateTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));
    }

    private void AppendLog(string eventName, GameState state, CoachAdvice? advice)
    {
        var items = state.PlayerItemIds.Count == 0
            ? "без предмета"
            : "Black Cleaver";

        var result = advice is null ? "нет правила" : advice.PowerState.ToString();
        EventLogListBox.Items.Insert(
            0,
            $"{DateTime.Now:HH:mm:ss}  {eventName} · {state.PlayerChampion} vs {state.OpponentChampion} · lvl {state.PlayerLevel} · {items} → {result}");
    }

    private static string GetSelectedChampion(ComboBox comboBox)
    {
        return comboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? string.Empty
            : comboBox.SelectedItem?.ToString() ?? string.Empty;
    }
}
