using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Состояние экрана текущего матча. Получает готовые обновления Application,
/// формирует представление и не знает о Riot DTO или WPF-контролах.
/// </summary>
public sealed class CurrentMatchViewModel : ObservableObject, IDisposable
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
    private bool _isDisposed;

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

    /// <summary>
    /// Сообщает оболочке, что очередной снимок полностью применён.
    /// Обычные PropertyChanged продолжают обслуживать WPF binding самого экрана.
    /// </summary>
    public event EventHandler? StateUpdated;

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
        ObjectDisposedException.ThrowIf(_isDisposed, this);
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
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_sessionCancellation.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var update = await _session.RefreshAsync(_sessionCancellation.Token);
            ApplyUpdate(update);
        }
        catch (OperationCanceledException) when (_sessionCancellation.IsCancellationRequested)
        {
            // Закрытие экрана штатно отменяет ручное обновление вместе с фоновым циклом.
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
        Stop();
        _sessionCancellation.Dispose();
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
        catch (Exception exception)
        {
            if (_isDisposed)
            {
                return;
            }

            Trace.TraceError(
                "Цикл текущего матча завершился с ошибкой ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            _lastUpdate = null;
            Status = MatchSnapshotReadStatus.Failed;
            RenderWaiting();
            StateUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyUpdate(ActiveMatchUpdate update)
    {
        if (_isDisposed)
        {
            return;
        }

        _lastUpdate = update;
        Status = update.Status;

        if (update.Snapshot is null)
        {
            RenderWaiting();
        }
        else
        {
            RenderSnapshot(update.Snapshot, update.CoachPackSelection);
        }

        StateUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void RenderSnapshot(
        MatchSnapshot snapshot,
        ActiveCoachPackSelection? selection)
    {
        var match = snapshot.Match;
        var activePlayer = match.GetActivePlayer();
        var matchup = MatchupResolver.Resolve(match);
        var advice = selection?.Advice;

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

        RenderAdvice(selection, activePlayer);
        RenderParticipants(match);
        RenderEvents(match, snapshot.Events);
    }

    private void RenderAdvice(
        ActiveCoachPackSelection? selection,
        PlayerState? activePlayer)
    {
        if (selection?.State == ActiveCoachPackSelectionState.Conflict)
        {
            RenderUnavailableAdvice(
                "Advice.PackConflict.Title",
                "Advice.PackConflict.Message",
                "Advice.PackConflict.Source",
                "Power.PackConflict",
                string.Join(", ", selection.ConflictingPackNames));
            return;
        }

        if (selection?.State == ActiveCoachPackSelectionState.LibraryUnavailable)
        {
            RenderUnavailableAdvice(
                "Advice.LibraryUnavailable.Title",
                "Advice.LibraryUnavailable.Message",
                "Advice.LibraryUnavailable.Source",
                "Power.LibraryUnavailable");
            return;
        }

        if (selection?.State == ActiveCoachPackSelectionState.MatchupUnavailable)
        {
            RenderUnavailableAdvice(
                "Advice.MatchupUnavailable.Title",
                "Advice.MatchupUnavailable.Message",
                "Advice.MatchupUnavailable.Source",
                "Power.NoRule");
            return;
        }

        if (selection is null || selection.State == ActiveCoachPackSelectionState.NotFound)
        {
            RenderUnavailableAdvice(
                "Advice.NoPack.Title",
                "Advice.NoPack.Message",
                "Advice.NoPack.Source",
                "Power.NoRule");
            return;
        }

        var advice = selection.Advice;
        if (advice is null)
        {
            AdviceTitle = _localization.Get("Advice.NoRule.Title");
            AdviceMessage = _localization.Format(
                "Advice.NoRule.Message",
                selection.Pack?.Name ?? string.Empty);
            AdviceReason = activePlayer is null
                ? _localization.Get("Advice.NoRule.Source")
                : _localization.Format("Current.Advice.LevelReason", activePlayer.Level);
            AdviceSource = selection.Pack is null
                ? string.Empty
                : _localization.Format("Current.Advice.Pack", selection.Pack.Name);
            PowerStateLabel = _localization.Get("Power.NoRule");
            PowerState = null;
            return;
        }

        AdviceTitle = advice.Title;
        AdviceMessage = advice.Message;
        AdviceReason = string.IsNullOrWhiteSpace(advice.WhyNow)
            ? activePlayer is null
                ? advice.Message
                : _localization.Format("Current.Advice.LevelReason", activePlayer.Level)
            : advice.WhyNow;
        AdviceSource = selection.Pack is null
            ? advice.Source
            : _localization.Format(
                "Current.Advice.PackAndSource",
                selection.Pack.Name,
                advice.Source);
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

    private void RenderUnavailableAdvice(
        string titleKey,
        string messageKey,
        string sourceKey,
        string powerKey,
        params object[] messageArguments)
    {
        AdviceTitle = _localization.Get(titleKey);
        AdviceMessage = messageArguments.Length == 0
            ? _localization.Get(messageKey)
            : _localization.Format(messageKey, messageArguments);
        AdviceReason = _localization.Get(sourceKey);
        AdviceSource = string.Empty;
        PowerStateLabel = _localization.Get(powerKey);
        PowerState = null;
    }

    private void RenderParticipants(MatchState match)
    {
        var activePlayer = match.GetActivePlayer();
        if (activePlayer is null || activePlayer.Team == TeamSide.Unknown)
        {
            ClearIfNotEmpty(Allies);
            ClearIfNotEmpty(Enemies);
            HasParticipants = false;
            OnPropertyChanged(nameof(ParticipantCount));
            return;
        }

        var allies = match.Players
            .Where(player => player.Team == activePlayer.Team)
            .Select(player => player with
            {
                Position = PlayerPositionResolver.Resolve(match, player)
            })
            .OrderBy(player => GetPositionOrder(player.Position))
            .Select(player => CreateParticipant(player, activePlayer.ParticipantId))
            .ToArray();
        var enemies = match.Players
            .Where(player => player.Team != TeamSide.Unknown
                && player.Team != activePlayer.Team)
            .Select(player => player with
            {
                Position = PlayerPositionResolver.Resolve(match, player)
            })
            .OrderBy(player => GetPositionOrder(player.Position))
            .Select(player => CreateParticipant(player, activePlayer.ParticipantId))
            .ToArray();

        ReplaceItemsIfChanged(Allies, allies);
        ReplaceItemsIfChanged(Enemies, enemies);

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
        var activePlayer = match.GetActivePlayer();
        var renderedEvents = events
            .OrderByDescending(item => item.GameTimeSeconds)
            .Select(matchEvent => CreateEvent(match, activePlayer, matchEvent))
            .ToArray();

        ReplaceItemsIfChanged(Events, renderedEvents);
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
        ClearIfNotEmpty(Allies);
        ClearIfNotEmpty(Enemies);
        ClearIfNotEmpty(Events);
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
            RenderSnapshot(snapshot, _lastUpdate.CoachPackSelection);
        }
        else
        {
            RenderWaiting();
        }

        StateUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static void ReplaceItemsIfChanged<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> items)
    {
        if (target.Count == items.Count && target.SequenceEqual(items))
        {
            return;
        }

        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static void ClearIfNotEmpty<T>(ObservableCollection<T> target)
    {
        if (target.Count > 0)
        {
            target.Clear();
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
