using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RiftCoach.Core;

namespace RiftCoach.App;

/// <summary>
/// Главное окно технического симулятора RiftCoach.
/// Оно собирает введённое состояние матча, передаёт его движку и показывает совет.
/// </summary>
public partial class MainWindow : Window
{
    // Riot использует числовые ID для предметов. ID Black Cleaver — 3071.
    private const int BlackCleaverItemId = 3071;

    // Стабильные ID нужны, чтобы общее состояние матча могло отличить участников.
    private const string SimulatedPlayerId = "simulated-player";
    private const string SimulatedOpponentId = "simulated-opponent";

    // Движок создаётся один раз при открытии окна и затем используется повторно.
    private readonly MatchupCoach _coach;

    public MainWindow()
    {
        // Метод создаётся WPF автоматически из разметки MainWindow.xaml.
        InitializeComponent();

        // AppContext.BaseDirectory указывает на папку, из которой запущено приложение.
        var packPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "CoachPacks",
            "urgot-vs-sett.json");

        // Загружаем правила из JSON и передаём их движку матчапов.
        var pack = CoachPackLoader.LoadFromFile(packPath);
        _coach = new MatchupCoach(pack.Rules);

        // После полной загрузки окна сразу показываем совет для стартового состояния.
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
        // HashSet хранит каждый ID только один раз и быстро проверяет наличие предмета.
        var itemIds = new HashSet<int>();
        if (BlackCleaverCheckBox.IsChecked == true)
        {
            itemIds.Add(BlackCleaverItemId);
        }

        // Симулятор создаёт тот же PlayerState, который позднее будет приходить из Riot API.
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

        // Сейчас тестовый матч содержит двух участников. Архитектура уже допускает
        // передачу полного списка из десяти игроков без изменения движка советов.
        var match = new MatchState
        {
            GameTimeSeconds = 0,
            ActivePlayerId = SimulatedPlayerId,
            Players = [player, opponent]
        };

        // Интерфейс не выбирает совет самостоятельно — он передаёт данные ядру.
        var advice = _coach.GetBestAdvice(match);
        ShowAdvice(advice);
        AppendLog(eventName, match, advice);
    }

    private void ShowAdvice(CoachAdvice? advice)
    {
        // Знак ? в CoachAdvice? означает, что подходящий совет может отсутствовать.
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

        // Для каждой оценки силы задаём отдельные текст и цвет индикатора.
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

    private void AppendLog(string eventName, MatchState match, CoachAdvice? advice)
    {
        var matchup = MatchupResolver.Resolve(match);
        if (matchup is null)
        {
            EventLogListBox.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {eventName} · матчап не определён");
            return;
        }

        // Тернарный оператор выбирает одно из двух текстовых значений по условию.
        var items = matchup.Player.ItemIds.Count == 0
            ? "без предмета"
            : "Black Cleaver";

        var result = advice is null ? "нет правила" : advice.PowerState.ToString();
        EventLogListBox.Items.Insert(
            0,
            $"{DateTime.Now:HH:mm:ss}  {eventName} · {matchup.Player.ChampionName} vs {matchup.Opponent.ChampionName} · lvl {matchup.Player.Level} · {items} → {result}");
    }

    private static string GetSelectedChampion(ComboBox comboBox)
    {
        // SelectedItem обычно содержит ComboBoxItem. Запасная ветка позволяет
        // позднее передавать в ComboBox и обычные строки.
        return comboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? string.Empty
            : comboBox.SelectedItem?.ToString() ?? string.Empty;
    }
}
