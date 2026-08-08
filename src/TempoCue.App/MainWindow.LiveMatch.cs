using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using TempoCue.Core;
using TempoCue.Riot;

namespace TempoCue.App;

/// <summary>Получение Live Client Data и отображение состояния активного матча.</summary>
public partial class MainWindow
{
    private enum ConnectionDisplayMode
    {
        Waiting,
        Connected,
        Error
    }

    private static readonly TimeSpan LiveDataPollingInterval = TimeSpan.FromSeconds(1);

    private readonly LiveClientDataClient _liveClientDataClient = new();
    private readonly CancellationTokenSource _livePollingCancellation = new();

    private bool _isLiveMatchConnected;
    private string? _lastLiveAdviceRuleId;
    private string? _connectionErrorKey;
    private object?[] _connectionErrorArguments = [];
    private Task? _livePollingTask;
    private MatchState? _latestLiveMatch;
    private LiveClientSnapshot? _latestLiveSnapshot;
    private ConnectionDisplayMode _connectionDisplayMode = ConnectionDisplayMode.Waiting;

    private async void HomeCheckButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RequestLiveSnapshotAsync(_livePollingCancellation.Token);
        }
        catch (OperationCanceledException) when (_livePollingCancellation.IsCancellationRequested)
        {
            // Закрытие окна может отменить ручную проверку одновременно с фоновым опросом.
        }
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
            _livePollingCancellation.Dispose();
            _liveClientDataClient.Dispose();
        }
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
        RenderHomeError(message);
    }

    private void RenderWaitingConnection()
    {
        SetLiveConnectionState(
            _localization.Get("Connection.WaitingMatch"),
            _localization.Get("Connection.NextPoll"),
            "#243550",
            "#AFC0D4");

        RenderHomeWaiting();
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

        RenderHomeConnected(activePlayer, gameTimeText, playersText);
        ShowAdvice(_coach.GetBestAdvice(match));
    }

    private void RenderHomeWaiting()
    {
        HomeConnectionTitleTextBlock.Text = _localization.Get("Connection.WaitingMatch");
        HomeConnectionDetailsTextBlock.Text = _localization.Get("Connection.PollingEverySecond");
        HomeHeroTitleTextBlock.Text = _localization.Get("Home.Waiting.Title");
        HomeHeroDescriptionTextBlock.Text = _localization.Get("Home.Waiting.Description");
        HomeLiveDataValueTextBlock.Text = _localization.Get("Home.Status.Waiting");
        HomeMatchStatusValueTextBlock.Text = _localization.Get("Home.Status.NotFound");
        HomeEngineStatusTextBlock.Text = _localization.Get("Home.Engine.Waiting");
        HomeMatchNavBadgeTextBlock.Text = "—";
        HomeGameTimePanel.Visibility = Visibility.Collapsed;
        ApplyHomeConnectionColors("#1E2E45", "#8393A8", "#B5C5D8", "#D6B766");
        UpdateTitleBarContext();
    }

    private void RenderHomeConnected(
        PlayerState? activePlayer,
        string gameTimeText,
        string playersText)
    {
        HomeConnectionTitleTextBlock.Text = _localization.Get("Connection.MatchConnected");
        HomeConnectionDetailsTextBlock.Text = activePlayer is null
            ? _localization.Format("Connection.ActivePlayerMissing", gameTimeText)
            : _localization.Format("Connection.PlayerSummary", activePlayer.ChampionName, gameTimeText, playersText);
        HomeHeroTitleTextBlock.Text = _localization.Get("Home.Connected.Title");
        HomeHeroDescriptionTextBlock.Text = _localization.Format(
            "Home.Connected.Description",
            activePlayer?.ChampionName ?? "—",
            playersText);
        HomeLiveDataValueTextBlock.Text = _localization.Get("Home.Status.Connected");
        HomeMatchStatusValueTextBlock.Text = _localization.Get("Home.Status.Active");
        HomeEngineStatusTextBlock.Text = _localization.Get("Home.Engine.Connected");
        HomeMatchNavBadgeTextBlock.Text = "LIVE";
        HomeGameTimeTextBlock.Text = gameTimeText;
        HomeGameTimePanel.Visibility = Visibility.Visible;
        ApplyHomeConnectionColors("#17342D", "#64D5A4", "#87E1BB", "#67CDA4");
        UpdateTitleBarContext(gameTimeText);
    }

    private void RenderHomeError(string message)
    {
        HomeConnectionTitleTextBlock.Text = _localization.Get("Connection.DataError");
        HomeConnectionDetailsTextBlock.Text = message;
        HomeHeroTitleTextBlock.Text = _localization.Get("Home.Error.Title");
        HomeHeroDescriptionTextBlock.Text = _localization.Format("Home.Error.Description", message);
        HomeLiveDataValueTextBlock.Text = _localization.Get("Home.Status.Error");
        HomeMatchStatusValueTextBlock.Text = _localization.Get("Home.Status.NotFound");
        HomeEngineStatusTextBlock.Text = _localization.Get("Home.Engine.Error");
        HomeMatchNavBadgeTextBlock.Text = "—";
        HomeGameTimePanel.Visibility = Visibility.Collapsed;
        ApplyHomeConnectionColors("#3A3020", "#E8BB58", "#F0CC7A", "#E8BB58");
        UpdateTitleBarContext();
    }

    private void ApplyHomeConnectionColors(
        string badgeBackground,
        string statusDot,
        string statusText,
        string valueText)
    {
        HomeConnectionBadgeBorder.Background = ToBrush(badgeBackground);
        HomeConnectionDot.Fill = ToBrush(statusDot);
        HomeEngineStatusDot.Fill = ToBrush(statusDot);
        HomeConnectionTitleTextBlock.Foreground = ToBrush(statusText);
        HomeLiveDataValueTextBlock.Foreground = ToBrush(valueText);
    }

    private void SetLiveConnectionState(
        string title,
        string details,
        string background,
        string foreground)
    {
        LiveConnectionTextBlock.Text = title;
        LiveConnectionDetailsTextBlock.Text = details;
        LiveConnectionBorder.Background = ToBrush(background);
        LiveConnectionTextBlock.Foreground = ToBrush(foreground);
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
}
