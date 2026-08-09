using System.Collections.ObjectModel;
using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Состояние экрана текущего матча. Получает готовые обновления Application,
/// формирует представление и не знает о Riot DTO или WPF-контролах.
/// </summary>
public sealed class CurrentMatchViewModel : ObservableObject
{
    private const int BlackCleaverItemId = 3071;

    private readonly ActiveMatchSession _session;
    private readonly LocalizationService _localization;
    private readonly CancellationTokenSource _sessionCancellation = new();

    private ActiveMatchUpdate? _lastUpdate;
    private Task? _watchTask;
    private MatchSnapshotReadStatus _status = MatchSnapshotReadStatus.NoActiveMatch;
    private string _connectionTitle = string.Empty;
    private string _connectionDetails = string.Empty;
    private string _gameTime = "00:00";
    private string _activeChampion = "—";
    private string _activeChampionInitials = "—";
    private string _activeSummary = "—";
    private string _activePosition = "—";
    private string _activeKda = "0/0/0";
    private string _activeCreepScore = "0 CS";
    private string _opponentChampion = "—";
    private string _opponentChampionInitials = "—";
    private string _matchupPhase = "—";
    private string _adviceTitle = string.Empty;
    private string _adviceMessage = string.Empty;
    private string _adviceReason = string.Empty;
    private string _adviceSource = string.Empty;
    private string _powerStateLabel = string.Empty;
    private PowerState? _powerState;
    private bool _hasParticipants;
    private bool _hasEvents;

    public CurrentMatchViewModel(
        ActiveMatchSession session,
        LocalizationService localization)
    {
        _session = session;
        _localization = localization;
        _localization.PropertyChanged += Localization_PropertyChanged;
        RenderWaiting();
    }

    public ObservableCollection<ParticipantViewModel> Allies { get; } = [];

    public ObservableCollection<ParticipantViewModel> Enemies { get; } = [];

    public ObservableCollection<MatchEventViewModel> Events { get; } = [];

    public MatchSnapshotReadStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string ConnectionTitle
    {
        get => _connectionTitle;
        private set => SetProperty(ref _connectionTitle, value);
    }

    public string ConnectionDetails
    {
        get => _connectionDetails;
        private set => SetProperty(ref _connectionDetails, value);
    }

    public string GameTime
    {
        get => _gameTime;
        private set => SetProperty(ref _gameTime, value);
    }

    public string ActiveChampion
    {
        get => _activeChampion;
        private set => SetProperty(ref _activeChampion, value);
    }

    public string ActiveChampionInitials
    {
        get => _activeChampionInitials;
        private set => SetProperty(ref _activeChampionInitials, value);
    }

    public string ActiveSummary
    {
        get => _activeSummary;
        private set => SetProperty(ref _activeSummary, value);
    }

    public string ActivePosition
    {
        get => _activePosition;
        private set => SetProperty(ref _activePosition, value);
    }

    public string ActiveKda
    {
        get => _activeKda;
        private set => SetProperty(ref _activeKda, value);
    }

    public string ActiveCreepScore
    {
        get => _activeCreepScore;
        private set => SetProperty(ref _activeCreepScore, value);
    }

    public string OpponentChampion
    {
        get => _opponentChampion;
        private set => SetProperty(ref _opponentChampion, value);
    }

    public string OpponentChampionInitials
    {
        get => _opponentChampionInitials;
        private set => SetProperty(ref _opponentChampionInitials, value);
    }

    public string MatchupPhase
    {
        get => _matchupPhase;
        private set => SetProperty(ref _matchupPhase, value);
    }

    public string AdviceTitle
    {
        get => _adviceTitle;
        private set => SetProperty(ref _adviceTitle, value);
    }

    public string AdviceMessage
    {
        get => _adviceMessage;
        private set => SetProperty(ref _adviceMessage, value);
    }

    public string AdviceReason
    {
        get => _adviceReason;
        private set => SetProperty(ref _adviceReason, value);
    }

    public string AdviceSource
    {
        get => _adviceSource;
        private set => SetProperty(ref _adviceSource, value);
    }

    public string PowerStateLabel
    {
        get => _powerStateLabel;
        private set => SetProperty(ref _powerStateLabel, value);
    }

    public PowerState? PowerState
    {
        get => _powerState;
        private set => SetProperty(ref _powerState, value);
    }

    public bool HasParticipants
    {
        get => _hasParticipants;
        private set => SetProperty(ref _hasParticipants, value);
    }

    public bool HasEvents
    {
        get => _hasEvents;
        private set => SetProperty(ref _hasEvents, value);
    }

    public int ParticipantCount => Allies.Count + Enemies.Count;

    public void Start()
    {
        _watchTask ??= WatchAsync(_sessionCancellation.Token);
    }

    public void Stop()
    {
        if (!_sessionCancellation.IsCancellationRequested)
        {
            _sessionCancellation.Cancel();
        }
    }

    public async Task RefreshAsync()
    {
        if (_sessionCancellation.IsCancellationRequested)
        {
            return;
        }

        var update = await _session.RefreshAsync(_sessionCancellation.Token);
        ApplyUpdate(update);
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var update in _session.WatchAsync(cancellationToken))
            {
                ApplyUpdate(update);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Закрытие приложения штатно завершает сессию наблюдения.
        }
    }

    private void ApplyUpdate(ActiveMatchUpdate update)
    {
        _lastUpdate = update;
        Status = update.Status;

        if (update.Snapshot is null)
        {
            RenderWaiting();
            return;
        }

        RenderSnapshot(update.Snapshot, update.Advice);
    }

    private void RenderSnapshot(MatchSnapshot snapshot, CoachAdvice? advice)
    {
        var match = snapshot.Match;
        var activePlayer = match.GetActivePlayer();
        var matchup = MatchupResolver.Resolve(match);

        ConnectionTitle = _localization.Get("Current.Status.Connected");
        ConnectionDetails = _localization.Format(
            "Current.Status.ConnectedDetails",
            _localization.Plural("Count.Player", match.Players.Count));
        GameTime = FormatTime(match.GameTimeSeconds);

        if (activePlayer is not null)
        {
            ActiveChampion = activePlayer.ChampionName;
            ActiveChampionInitials = GetInitials(activePlayer.ChampionName);
            ActiveSummary = _localization.Format(
                "Current.Player.Level",
                activePlayer.ChampionName,
                activePlayer.Level);
            var resolvedPosition = PlayerPositionResolver.Resolve(match, activePlayer);
            ActivePosition = GetPositionLabel(resolvedPosition);
            ActiveKda = $"{activePlayer.Kills}/{activePlayer.Deaths}/{activePlayer.Assists}";
            ActiveCreepScore = FormatCreepScore(activePlayer.CreepScore);
            MatchupPhase = GetPhaseLabel(activePlayer, advice);
        }
        else
        {
            ResetActivePlayer();
        }

        if (matchup is not null)
        {
            OpponentChampion = matchup.Opponent.ChampionName;
            OpponentChampionInitials = GetInitials(matchup.Opponent.ChampionName);
        }
        else
        {
            OpponentChampion = "—";
            OpponentChampionInitials = "—";
        }

        RenderAdvice(advice, activePlayer);
        RenderParticipants(match);
        RenderEvents(match, snapshot.Events);
    }

    private void RenderAdvice(CoachAdvice? advice, PlayerState? activePlayer)
    {
        if (advice is null)
        {
            AdviceTitle = _localization.Get("Advice.NoRule.Title");
            AdviceMessage = _localization.Get("Advice.NoRule.Message");
            AdviceReason = _localization.Get("Advice.NoRule.Source");
            AdviceSource = string.Empty;
            PowerStateLabel = _localization.Get("Power.NoRule");
            PowerState = null;
            return;
        }

        AdviceTitle = advice.Title;
        AdviceMessage = advice.Message;
        AdviceReason = activePlayer is null
            ? advice.Message
            : _localization.Format(
                "Current.Advice.LevelReason",
                activePlayer.Level);
        AdviceSource = advice.Source;
        PowerState = advice.PowerState;
        PowerStateLabel = advice.PowerState switch
        {
            TempoCue.Core.PowerState.Dangerous => _localization.Get("Power.Dangerous"),
            TempoCue.Core.PowerState.Cautious => _localization.Get("Power.Cautious"),
            TempoCue.Core.PowerState.Playable => _localization.Get("Power.Playable"),
            TempoCue.Core.PowerState.Favorable => _localization.Get("Power.Favorable"),
            _ => throw new ArgumentOutOfRangeException(nameof(advice.PowerState))
        };
    }

    private void RenderParticipants(MatchState match)
    {
        Allies.Clear();
        Enemies.Clear();

        var activePlayer = match.GetActivePlayer();
        if (activePlayer is null || activePlayer.Team == TeamSide.Unknown)
        {
            HasParticipants = false;
            OnPropertyChanged(nameof(ParticipantCount));
            return;
        }

        foreach (var player in match.Players
                     .Where(player => player.Team == activePlayer.Team)
                     .Select(player => player with
                     {
                         Position = PlayerPositionResolver.Resolve(match, player)
                     })
                     .OrderBy(player => GetPositionOrder(player.Position)))
        {
            Allies.Add(CreateParticipant(player, activePlayer.ParticipantId));
        }

        foreach (var player in match.Players
                     .Where(player => player.Team != TeamSide.Unknown
                         && player.Team != activePlayer.Team)
                     .Select(player => player with
                     {
                         Position = PlayerPositionResolver.Resolve(match, player)
                     })
                     .OrderBy(player => GetPositionOrder(player.Position)))
        {
            Enemies.Add(CreateParticipant(player, activePlayer.ParticipantId));
        }

        HasParticipants = Allies.Count + Enemies.Count > 0;
        OnPropertyChanged(nameof(ParticipantCount));
    }

    private ParticipantViewModel CreateParticipant(
        PlayerState player,
        string activePlayerId)
    {
        var isActive = string.Equals(
            player.ParticipantId,
            activePlayerId,
            StringComparison.OrdinalIgnoreCase);
        var state = player.IsDead
            ? _localization.Format(
                "Current.Participant.Dead",
                Math.Max(0, (int)Math.Ceiling(player.RespawnTimerSeconds)))
            : _localization.Get("Current.Participant.Alive");
        var details = isActive
            ? _localization.Format(
                "Current.Participant.You",
                FormatCreepScore(player.CreepScore))
            : _localization.Format(
                "Current.Participant.State",
                state,
                FormatCreepScore(player.CreepScore));

        return new ParticipantViewModel(
            GetPositionCode(player.Position),
            GetInitials(player.ChampionName),
            player.ChampionName,
            details,
            $"{player.Kills}/{player.Deaths}/{player.Assists}",
            player.Level,
            isActive,
            player.IsDead);
    }

    private void RenderEvents(MatchState match, IReadOnlyList<MatchEvent> events)
    {
        Events.Clear();
        var activePlayer = match.GetActivePlayer();

        foreach (var matchEvent in events.OrderByDescending(item => item.GameTimeSeconds))
        {
            Events.Add(CreateEvent(match, activePlayer, matchEvent));
        }

        HasEvents = Events.Count > 0;
    }

    private MatchEventViewModel CreateEvent(
        MatchState match,
        PlayerState? activePlayer,
        MatchEvent matchEvent)
    {
        if (matchEvent.Kind == MatchEventKind.DragonKill)
        {
            var killer = FindPlayer(match, matchEvent.KillerParticipantId);
            var isAlly = killer is not null
                && activePlayer is not null
                && killer.Team == activePlayer.Team;
            var titleKey = killer is null
                ? "Current.Event.Dragon.UnknownTeam"
                : isAlly
                    ? "Current.Event.Dragon.Allies"
                    : "Current.Event.Dragon.Enemies";

            return new MatchEventViewModel(
                FormatTime(matchEvent.GameTimeSeconds),
                _localization.Format(titleKey, GetDragonLabel(matchEvent.DragonType)),
                _localization.Get("Current.Event.RiotFact"),
                MatchEventAccent.Objective);
        }

        var killerPlayer = FindPlayer(match, matchEvent.KillerParticipantId);
        var victimPlayer = FindPlayer(match, matchEvent.VictimParticipantId);
        var killerName = killerPlayer?.ChampionName
            ?? _localization.Get("Current.Event.UnknownChampion");
        var victimName = victimPlayer?.ChampionName
            ?? _localization.Get("Current.Event.UnknownChampion");
        var title = killerPlayer is null
            ? _localization.Format("Current.Event.Death", victimName)
            : _localization.Format("Current.Event.Kill", killerName, victimName);
        var accent = victimPlayer is null || activePlayer is null
            ? MatchEventAccent.Neutral
            : victimPlayer.Team == activePlayer.Team
                ? MatchEventAccent.Ally
                : MatchEventAccent.Enemy;

        return new MatchEventViewModel(
            FormatTime(matchEvent.GameTimeSeconds),
            title,
            _localization.Get("Current.Event.RiotFact"),
            accent);
    }

    private void RenderWaiting()
    {
        ConnectionTitle = Status switch
        {
            MatchSnapshotReadStatus.GameLoading => _localization.Get("Current.Status.Loading"),
            MatchSnapshotReadStatus.Timeout => _localization.Get("Current.Status.Timeout"),
            MatchSnapshotReadStatus.InvalidData => _localization.Get("Current.Status.InvalidData"),
            MatchSnapshotReadStatus.Failed => _localization.Get("Current.Status.Error"),
            _ => _localization.Get("Current.Status.Waiting")
        };
        ConnectionDetails = Status == MatchSnapshotReadStatus.GameLoading
            ? _localization.Get("Current.Status.LoadingDetails")
            : _localization.Get("Current.Status.WaitingDetails");
        GameTime = "00:00";
        ActiveChampion = "—";
        ActiveChampionInitials = "—";
        ActiveSummary = _localization.Get("Current.Player.Waiting");
        ActivePosition = "—";
        ActiveKda = "0/0/0";
        ActiveCreepScore = "0 CS";
        OpponentChampion = "—";
        OpponentChampionInitials = "—";
        MatchupPhase = "—";
        AdviceTitle = _localization.Get("Advice.Waiting.Title");
        AdviceMessage = _localization.Get("Advice.Waiting.Message");
        AdviceReason = _localization.Get("Current.Advice.WaitingReason");
        AdviceSource = string.Empty;
        PowerStateLabel = _localization.Get("Power.Waiting");
        PowerState = null;
        Allies.Clear();
        Enemies.Clear();
        Events.Clear();
        HasParticipants = false;
        HasEvents = false;
        OnPropertyChanged(nameof(ParticipantCount));
    }

    private void Localization_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]")
        {
            return;
        }

        if (_lastUpdate?.Snapshot is { } snapshot)
        {
            RenderSnapshot(snapshot, _lastUpdate.Advice);
        }
        else
        {
            RenderWaiting();
        }
    }

    private string GetPhaseLabel(PlayerState player, CoachAdvice? advice)
    {
        if (advice?.RequiredItemIds.Contains(BlackCleaverItemId) == true)
        {
            return _localization.Format("Current.Phase.LevelWithCleaver", player.Level);
        }

        if (advice?.ExcludedItemIds.Contains(BlackCleaverItemId) == true)
        {
            return _localization.Format("Current.Phase.LevelWithoutCleaver", player.Level);
        }

        return _localization.Format("Current.Phase.Level", player.Level);
    }

    private void ResetActivePlayer()
    {
        ActiveChampion = "—";
        ActiveChampionInitials = "—";
        ActiveSummary = _localization.Get("Current.Player.Waiting");
        ActivePosition = "—";
        ActiveKda = "0/0/0";
        ActiveCreepScore = "0 CS";
        MatchupPhase = "—";
    }

    private string GetPositionLabel(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => _localization.Get("Position.Top"),
        PlayerPosition.Jungle => _localization.Get("Position.Jungle"),
        PlayerPosition.Middle => _localization.Get("Position.Middle"),
        PlayerPosition.Bottom => _localization.Get("Position.Bottom"),
        PlayerPosition.Support => _localization.Get("Position.Support"),
        _ => _localization.Get("Position.Unknown")
    };

    private string FormatCreepScore(int creepScore) =>
        _localization.Format("Current.CreepScore.Approximate", creepScore);

    private string GetDragonLabel(DragonType dragonType) =>
        _localization.Get($"Dragon.{dragonType}");

    private static PlayerState? FindPlayer(MatchState match, string? participantId)
    {
        return participantId is null
            ? null
            : match.Players.FirstOrDefault(player => string.Equals(
                player.ParticipantId,
                participantId,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string GetPositionCode(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => "TOP",
        PlayerPosition.Jungle => "JGL",
        PlayerPosition.Middle => "MID",
        PlayerPosition.Bottom => "BOT",
        PlayerPosition.Support => "SUP",
        _ => "—"
    };

    private static int GetPositionOrder(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => 0,
        PlayerPosition.Jungle => 1,
        PlayerPosition.Middle => 2,
        PlayerPosition.Bottom => 3,
        PlayerPosition.Support => 4,
        _ => 5
    };

    private static string GetInitials(string championName)
    {
        var letters = championName.Where(char.IsLetter).Take(3).ToArray();
        return letters.Length == 0
            ? "—"
            : new string(letters).ToUpperInvariant();
    }

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
}
