using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RiftCoach.Core;
using RiftCoach.Riot;

namespace RiftCoach.App;

/// <summary>
/// Главное окно технического симулятора RiftCoach.
/// Оно собирает введённое состояние матча, передаёт его движку и показывает совет.
/// </summary>
public partial class MainWindow : Window
{
    // Одного запроса в секунду достаточно для уровня, предметов и состояния матча.
    private static readonly TimeSpan LiveDataPollingInterval = TimeSpan.FromSeconds(1);

    // Riot использует числовые ID для предметов. ID Black Cleaver — 3071.
    private const int BlackCleaverItemId = 3071;

    // Стабильные ID нужны, чтобы общее состояние матча могло отличить участников.
    private const string SimulatedPlayerId = "simulated-player";
    private const string SimulatedOpponentId = "simulated-opponent";

    // Движок создаётся один раз при открытии окна и затем используется повторно.
    private readonly MatchupCoach _coach;

    // Клиент работает только с локальным API запущенной League of Legends.
    private readonly LiveClientDataClient _liveClientDataClient = new();
    private readonly CancellationTokenSource _livePollingCancellation = new();

    private bool _isLiveMatchConnected;
    private string? _lastLiveAdviceRuleId;
    private Task? _livePollingTask;
    private MatchState? _latestLiveMatch;

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

        // После загрузки сохраняем ручной симулятор и параллельно начинаем искать матч.
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ShowInitialWaitingAdvice();

        // Опрос работает в фоне жизненного цикла окна. Все ожидаемые ошибки
        // обрабатываются внутри метода, поэтому приложение продолжает ждать матч.
        _livePollingTask ??= PollLiveMatchAsync(_livePollingCancellation.Token);
    }

    private void ShowInitialWaitingAdvice()
    {
        SetManualSimulationEnabled(isEnabled: true);
        AdviceTitleTextBlock.Text = "Ожидание активного матча";
        AdviceMessageTextBlock.Text =
            "RiftCoach опрашивает локальный Riot API каждую секунду. "
            + "После загрузки на карту здесь появится совет для фактического состояния игры.";
        AdviceSourceTextBlock.Text =
            "Ручной симулятор слева остаётся доступен для проверки Coach Pack без матча.";
        RuleIdTextBlock.Text = "RULE: —";
        ApplyPowerState("ОЖИДАНИЕ", "#243550", "#AFC0D4");
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        _livePollingCancellation.Cancel();

        try
        {
            if (_livePollingTask is not null)
            {
                await _livePollingTask;
            }
        }
        finally
        {
            // Ресурсы HTTP-клиента освобождаются только после завершения запроса,
            // чтобы закрытие окна не создало гонку с фоновым опросом.
            _livePollingCancellation.Dispose();
            _liveClientDataClient.Dispose();
        }
    }

    private void EvaluateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLiveMatchConnected && _latestLiveMatch is not null)
        {
            var liveAdvice = _coach.GetBestAdvice(_latestLiveMatch);
            ShowAdvice(liveAdvice);
            AppendLog("Ручная проверка Riot API", _latestLiveMatch, liveAdvice);
            return;
        }

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

    private void CopyRawJsonButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(RawJsonTextBox.Text))
        {
            Clipboard.SetText(RawJsonTextBox.Text);
            LiveJsonInfoTextBlock.Text = "JSON скопирован в буфер обмена";
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

    private async Task PollLiveMatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await RequestLiveSnapshotAsync(cancellationToken);
                await Task.Delay(LiveDataPollingInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Закрытие окна штатно отменяет бесконечный цикл опроса.
        }
    }

    private async Task RequestLiveSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _liveClientDataClient.GetSnapshotAsync(cancellationToken);
            ApplyLiveSnapshot(snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            ShowLiveConnectionProblem("Riot API не ответил за 3 секунды");
        }
        catch (HttpRequestException)
        {
            // Отказ подключения — нормальное состояние, пока игра не запущена.
            ShowWaitingForLiveMatch();
        }
        catch (JsonException exception)
        {
            ShowLiveConnectionProblem($"Не удалось прочитать JSON: {exception.Message}");
        }
        catch (InvalidDataException exception)
        {
            ShowLiveConnectionProblem(exception.Message);
        }
    }

    private void ApplyLiveSnapshot(LiveClientSnapshot snapshot)
    {
        var match = snapshot.Match;
        var activePlayer = match.GetActivePlayer();
        var gameTime = TimeSpan.FromSeconds(Math.Max(0, match.GameTimeSeconds));

        _isLiveMatchConnected = true;
        _latestLiveMatch = match;
        SynchronizeLiveStateControls(match);
        var previousVerticalOffset = RawJsonTextBox.VerticalOffset;
        var previousHorizontalOffset = RawJsonTextBox.HorizontalOffset;
        RawJsonTextBox.Text = snapshot.RawJson;
        RawJsonTextBox.ScrollToVerticalOffset(previousVerticalOffset);
        RawJsonTextBox.ScrollToHorizontalOffset(previousHorizontalOffset);
        LiveJsonInfoTextBlock.Text =
            $"Обновлено {snapshot.ReceivedAtUtc.ToLocalTime():HH:mm:ss} · "
            + $"{match.Players.Count} участников · период 1 с";

        SetLiveConnectionState(
            "МАТЧ ПОДКЛЮЧЕН",
            activePlayer is null
                ? $"{gameTime.Minutes:00}:{gameTime.Seconds:00} · активный игрок не найден"
                : $"{activePlayer.ChampionName} · {gameTime.Minutes:00}:{gameTime.Seconds:00} · {match.Players.Count} игроков",
            "#19362F",
            "#76D7A8");

        var advice = _coach.GetBestAdvice(match);
        ShowAdvice(advice);

        // В журнал попадает только смена правила, хотя JSON обновляется каждую секунду.
        var currentRuleId = advice?.RuleId ?? "no-rule";
        if (!string.Equals(currentRuleId, _lastLiveAdviceRuleId, StringComparison.Ordinal))
        {
            AppendLog("Riot API", match, advice);
            _lastLiveAdviceRuleId = currentRuleId;
        }
    }

    private void ShowWaitingForLiveMatch()
    {
        if (_isLiveMatchConnected)
        {
            EventLogListBox.Items.Insert(
                0,
                $"{DateTime.Now:HH:mm:ss}  Riot API · активный матч завершён или соединение потеряно");

            LiveJsonInfoTextBlock.Text =
                "Активный матч недоступен · ниже сохранён последний успешный JSON";
        }

        _isLiveMatchConnected = false;
        _latestLiveMatch = null;
        _lastLiveAdviceRuleId = null;
        SetManualSimulationEnabled(isEnabled: true);

        SetLiveConnectionState(
            "ОЖИДАНИЕ МАТЧА",
            "Следующий запрос через 1 с",
            "#243550",
            "#AFC0D4");
    }

    private void ShowLiveConnectionProblem(string message)
    {
        SetLiveConnectionState(
            "ОШИБКА ДАННЫХ",
            message,
            "#3B3421",
            "#F3C969");

        LiveJsonInfoTextBlock.Text = $"Ошибка последнего запроса · {message}";
    }

    private void SetLiveConnectionState(
        string title,
        string details,
        string background,
        string foreground)
    {
        LiveConnectionTextBlock.Text = title;
        LiveConnectionDetailsTextBlock.Text = details;
        LiveConnectionBorder.Background =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
        LiveConnectionTextBlock.Foreground =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(foreground));
    }

    private void SynchronizeLiveStateControls(MatchState match)
    {
        var activePlayer = match.GetActivePlayer();
        if (activePlayer is null)
        {
            return;
        }

        var matchup = MatchupResolver.Resolve(match);

        SetSelectedChampion(PlayerChampionComboBox, activePlayer.ChampionName);
        if (matchup is not null)
        {
            SetSelectedChampion(OpponentChampionComboBox, matchup.Opponent.ChampionName);
        }

        LevelSlider.Value = Math.Clamp(activePlayer.Level, 1, 18);
        BlackCleaverCheckBox.IsChecked = activePlayer.ItemIds.Contains(BlackCleaverItemId);
        SetManualSimulationEnabled(isEnabled: false);
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
            ? "Проверить симуляцию"
            : "Проверить активный матч";
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

        var items = matchup.Player.ItemIds.Count switch
        {
            0 => "без предметов",
            _ when matchup.Player.ItemIds.Contains(BlackCleaverItemId) => "Black Cleaver",
            _ => $"предметов: {matchup.Player.ItemIds.Count}"
        };

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
