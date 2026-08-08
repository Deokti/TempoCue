using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.Core;
using TempoCue.Riot;

namespace TempoCue.App;

/// <summary>
/// Главное окно технического симулятора TempoCue.
/// Оно собирает введённое состояние матча, передаёт его движку и показывает совет.
/// </summary>
public partial class MainWindow : Window
{
    private enum ConnectionDisplayMode
    {
        Waiting,
        Connected,
        Error
    }

    // Одного запроса в секунду достаточно для уровня, предметов и состояния матча.
    private static readonly TimeSpan LiveDataPollingInterval = TimeSpan.FromSeconds(1);

    // Riot использует числовые ID для предметов. ID Black Cleaver — 3071.
    private const int BlackCleaverItemId = 3071;

    // Стабильные ID нужны, чтобы общее состояние матча могло отличить участников.
    private const string SimulatedPlayerId = "simulated-player";
    private const string SimulatedOpponentId = "simulated-opponent";

    // Движок создаётся один раз при открытии окна и затем используется повторно.
    private readonly MatchupCoach _coach;
    private readonly LocalizationService _localization = LocalizationService.Instance;

    // Клиент работает только с локальным API запущенной League of Legends.
    private readonly LiveClientDataClient _liveClientDataClient = new();
    private readonly CancellationTokenSource _livePollingCancellation = new();

    private bool _isLiveMatchConnected;
    private bool _isWindowInitialized;
    private string? _lastLiveAdviceRuleId;
    private string? _connectionErrorKey;
    private object?[] _connectionErrorArguments = [];
    private Task? _livePollingTask;
    private MatchState? _latestLiveMatch;
    private MatchState? _lastAdviceMatch;
    private LiveClientSnapshot? _latestLiveSnapshot;
    private ConnectionDisplayMode _connectionDisplayMode = ConnectionDisplayMode.Waiting;

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

        // Названия языков показаны на самих языках: «Русский» и «English».
        // Поэтому эти два элемента не требуют отдельного перевода.
        LanguageComboBox.SelectedValue = _localization.CurrentLanguageCode;
        _isWindowInitialized = true;

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
        AdviceTitleTextBlock.Text = _localization.Get("Advice.Waiting.Title");
        AdviceMessageTextBlock.Text = _localization.Get("Advice.Waiting.Message");
        AdviceSourceTextBlock.Text = _localization.Get("Advice.Waiting.Source");
        RuleIdTextBlock.Text = _localization.Get("Diagnostics.RuleEmpty");
        ApplyPowerState(_localization.Get("Power.Waiting"), "#243550", "#AFC0D4");
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

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowInitialized || LanguageComboBox.SelectedValue is not string languageCode)
        {
            return;
        }

        _localization.SetLanguage(languageCode);
        AppSettingsService.SaveLanguage(languageCode);
        RefreshLocalizedContent();
    }

    /// <summary>
    /// Статические подписи обновляются Binding-ами автоматически. Этот метод
    /// повторно формирует строки, в которые подставляются данные текущего матча.
    /// </summary>
    private void RefreshLocalizedContent()
    {
        SetManualSimulationEnabled(isEnabled: !_isLiveMatchConnected);

        switch (_connectionDisplayMode)
        {
            case ConnectionDisplayMode.Connected when _latestLiveSnapshot is not null:
                RenderLiveSnapshot(_latestLiveSnapshot);
                break;
            case ConnectionDisplayMode.Error:
                RenderConnectionProblem();
                break;
            default:
                RenderWaitingConnection();
                break;
        }

        if (_connectionDisplayMode != ConnectionDisplayMode.Connected)
        {
            if (_lastAdviceMatch is null)
            {
                ShowInitialWaitingAdvice();
            }
            else
            {
                ShowAdvice(_coach.GetBestAdvice(_lastAdviceMatch));
            }
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
        _lastAdviceMatch = match;
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
            ShowLiveConnectionProblem("Error.Timeout");
        }
        catch (HttpRequestException)
        {
            // Отказ подключения — нормальное состояние, пока игра не запущена.
            ShowWaitingForLiveMatch();
        }
        catch (JsonException exception)
        {
            ShowLiveConnectionProblem("Error.JsonRead", exception.Message);
        }
        catch (InvalidDataException)
        {
            ShowLiveConnectionProblem("Error.InvalidData");
        }
    }

    private void ApplyLiveSnapshot(LiveClientSnapshot snapshot)
    {
        var match = snapshot.Match;

        _isLiveMatchConnected = true;
        _latestLiveMatch = match;
        _latestLiveSnapshot = snapshot;
        _lastAdviceMatch = match;
        _connectionDisplayMode = ConnectionDisplayMode.Connected;
        _connectionErrorKey = null;
        _connectionErrorArguments = [];
        SynchronizeLiveStateControls(match);
        var previousVerticalOffset = RawJsonTextBox.VerticalOffset;
        var previousHorizontalOffset = RawJsonTextBox.HorizontalOffset;
        RawJsonTextBox.Text = snapshot.RawJson;
        RawJsonTextBox.ScrollToVerticalOffset(previousVerticalOffset);
        RawJsonTextBox.ScrollToHorizontalOffset(previousHorizontalOffset);
        RenderLiveSnapshot(snapshot);

        // В журнал попадает только смена правила, хотя JSON обновляется каждую секунду.
        var advice = _coach.GetBestAdvice(match);
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
                _localization.Format("Log.MatchEnded", DateTime.Now.ToString("HH:mm:ss")));

            LiveJsonInfoTextBlock.Text = _localization.Get("Json.MatchUnavailable");
        }

        _isLiveMatchConnected = false;
        _latestLiveMatch = null;
        _latestLiveSnapshot = null;
        _lastLiveAdviceRuleId = null;
        _connectionDisplayMode = ConnectionDisplayMode.Waiting;
        _connectionErrorKey = null;
        _connectionErrorArguments = [];
        SetManualSimulationEnabled(isEnabled: true);
        RenderWaitingConnection();
    }

    private void ShowLiveConnectionProblem(string messageKey, params object?[] arguments)
    {
        _connectionDisplayMode = ConnectionDisplayMode.Error;
        _connectionErrorKey = messageKey;
        _connectionErrorArguments = arguments;
        RenderConnectionProblem();
    }

    private void RenderConnectionProblem()
    {
        var message = _connectionErrorKey is null
            ? _localization.Get("Error.InvalidData")
            : _localization.Format(_connectionErrorKey, _connectionErrorArguments);

        SetLiveConnectionState(
            _localization.Get("Connection.DataError"),
            message,
            "#3B3421",
            "#F3C969");

        LiveJsonInfoTextBlock.Text = _localization.Format("Json.LastRequestError", message);
    }

    private void RenderWaitingConnection()
    {
        SetLiveConnectionState(
            _localization.Get("Connection.WaitingMatch"),
            _localization.Get("Connection.NextPoll"),
            "#243550",
            "#AFC0D4");
    }

    private void RenderLiveSnapshot(LiveClientSnapshot snapshot)
    {
        var match = snapshot.Match;
        var activePlayer = match.GetActivePlayer();
        var gameTime = TimeSpan.FromSeconds(Math.Max(0, match.GameTimeSeconds));
        var gameTimeText = $"{gameTime.Minutes:00}:{gameTime.Seconds:00}";
        var playersText = _localization.Plural("Count.Player", match.Players.Count);

        LiveJsonInfoTextBlock.Text = _localization.Format(
            "Json.Updated",
            snapshot.ReceivedAtUtc.ToLocalTime().ToString("HH:mm:ss"),
            playersText);

        var details = activePlayer is null
            ? _localization.Format("Connection.ActivePlayerMissing", gameTimeText)
            : _localization.Format(
                "Connection.PlayerSummary",
                activePlayer.ChampionName,
                gameTimeText,
                playersText);

        SetLiveConnectionState(
            _localization.Get("Connection.MatchConnected"),
            details,
            "#19362F",
            "#76D7A8");

        ShowAdvice(_coach.GetBestAdvice(match));
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
            ? _localization.Get("Action.CheckSimulation")
            : _localization.Get("Action.CheckLiveMatch");
    }

    private void ShowAdvice(CoachAdvice? advice)
    {
        // Знак ? в CoachAdvice? означает, что подходящий совет может отсутствовать.
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

        // Для каждой оценки силы задаём отдельные текст и цвет индикатора.
        switch (advice.PowerState)
        {
            case PowerState.Dangerous:
                ApplyPowerState(_localization.Get("Power.Dangerous"), "#3B2630", "#FF8E9D");
                break;
            case PowerState.Cautious:
                ApplyPowerState(_localization.Get("Power.Cautious"), "#3B3421", "#F3C969");
                break;
            case PowerState.Playable:
                ApplyPowerState(_localization.Get("Power.Playable"), "#19362F", "#76D7A8");
                break;
            case PowerState.Favorable:
                ApplyPowerState(_localization.Get("Power.Favorable"), "#173548", "#76C9F0");
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
