using System.Collections.ObjectModel;
using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.App.Profiles;
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

    private readonly IActiveMatchMonitor _monitor;
    private readonly IChampionCatalog _championCatalog;
    private readonly ILeagueClientRegionSource _leagueClientRegionSource;
    private readonly LocalizationService _localization;

    private ActiveMatchUpdate? _lastUpdate;
    private IReadOnlyDictionary<string, Uri> _championImages =
        new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _championCatalogLoadCancellation;
    private CancellationTokenSource? _profileRegionLoadCancellation;
    private int _championCatalogLoadVersion;
    private int _profileRegionLoadVersion;
    private bool _isChampionCatalogLoaded;
    private bool _isProfileRegionLoaded;
    private string? _leagueClientRegion;
    private MatchSnapshotReadStatus _status = MatchSnapshotReadStatus.NoActiveMatch;
    private string _connectionTitle = string.Empty;
    private string _connectionDetails = string.Empty;
    private string _stateTitle = string.Empty;
    private string _stateMessage = string.Empty;
    private bool _hasCurrentMatch;
    private bool _canRetry;
    private string _gameTime = "—";
    private string _activeChampion = "—";
    private string _activeChampionInitials = "—";
    private Uri? _activeChampionImageUri;
    private string _activeSummary = "—";
    private string _activePosition = "—";
    private string _activeKda = "0/0/0";
    private string _activeCreepScore = "0 CS";
    private string _opponentChampion = "—";
    private string _opponentChampionInitials = "—";
    private Uri? _opponentChampionImageUri;
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
        IActiveMatchMonitor monitor,
        IChampionCatalog championCatalog,
        ILeagueClientRegionSource leagueClientRegionSource,
        LocalizationService localization)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _championCatalog = championCatalog ?? throw new ArgumentNullException(nameof(championCatalog));
        _leagueClientRegionSource = leagueClientRegionSource
            ?? throw new ArgumentNullException(nameof(leagueClientRegionSource));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _monitor.Updated += Monitor_Updated;
        _localization.PropertyChanged += Localization_PropertyChanged;
        RenderWaiting();

        if (_monitor.CurrentUpdate is { } update)
        {
            ApplyUpdate(update);
        }
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

    public Uri? ActiveChampionImageUri
    {
        get => _activeChampionImageUri;
        private set => SetProperty(ref _activeChampionImageUri, value);
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

    public Uri? OpponentChampionImageUri
    {
        get => _opponentChampionImageUri;
        private set => SetProperty(ref _opponentChampionImageUri, value);
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

    public Task RefreshAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return _monitor.RefreshAsync();
    }

    /// <summary>
    /// Один раз загружает каталог портретов. При недоступности Data Dragon экран
    /// продолжает показывать инициалы и может повторить загрузку позднее.
    /// </summary>
    public async Task LoadChampionImagesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isChampionCatalogLoaded)
        {
            return;
        }

        var version = Interlocked.Increment(ref _championCatalogLoadVersion);
        var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previousCancellation = Interlocked.Exchange(
            ref _championCatalogLoadCancellation,
            requestCancellation);
        TryCancel(previousCancellation);

        try
        {
            var result = await _championCatalog.LoadAsync(
                _localization.CurrentLanguageCode,
                requestCancellation.Token);
            if (_isDisposed || version != Volatile.Read(ref _championCatalogLoadVersion))
            {
                return;
            }

            _championImages = BuildChampionImageLookup(result.Champions);
            _isChampionCatalogLoaded = result.IsAvailable;
            RerenderLastUpdate();
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested
            && requestCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.CompareExchange(
                ref _championCatalogLoadCancellation,
                null,
                requestCancellation);
            requestCancellation.Dispose();
        }
    }

    public string StateTitle
    {
        get => _stateTitle;
        private set => SetProperty(ref _stateTitle, value);
    }

    public string StateMessage
    {
        get => _stateMessage;
        private set => SetProperty(ref _stateMessage, value);
    }

    public bool HasCurrentMatch
    {
        get => _hasCurrentMatch;
        private set => SetProperty(ref _hasCurrentMatch, value);
    }

    public bool CanRetry
    {
        get => _canRetry;
        private set => SetProperty(ref _canRetry, value);
    }

    /// <summary>
    /// Один раз читает регион локального клиента. Если регион неизвестен, Riot ID всё равно
    /// показывается, но ссылка не строится по догадке.
    /// </summary>
    public async Task LoadPlayerProfileRegionAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isProfileRegionLoaded)
        {
            return;
        }

        var version = Interlocked.Increment(ref _profileRegionLoadVersion);
        var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previousCancellation = Interlocked.Exchange(
            ref _profileRegionLoadCancellation,
            requestCancellation);
        TryCancel(previousCancellation);

        try
        {
            var region = await _leagueClientRegionSource
                .ReadRegionAsync(requestCancellation.Token);
            if (_isDisposed || version != Volatile.Read(ref _profileRegionLoadVersion))
            {
                return;
            }

            _leagueClientRegion = region;
            _isProfileRegionLoaded = true;
            RerenderLastUpdate();
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested
            && requestCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.CompareExchange(
                ref _profileRegionLoadCancellation,
                null,
                requestCancellation);
            requestCancellation.Dispose();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Interlocked.Increment(ref _championCatalogLoadVersion);
        Interlocked.Increment(ref _profileRegionLoadVersion);
        TryCancel(Interlocked.Exchange(ref _championCatalogLoadCancellation, null));
        TryCancel(Interlocked.Exchange(ref _profileRegionLoadCancellation, null));
        _monitor.Updated -= Monitor_Updated;
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private void Monitor_Updated(object? sender, ActiveMatchUpdateEventArgs e) =>
        ApplyUpdate(e.Update);

    private void ApplyUpdate(ActiveMatchUpdate update)
    {
        if (_isDisposed)
        {
            return;
        }

        _lastUpdate = update;
        Status = update.Status;

        if (update.Status != MatchSnapshotReadStatus.Success || update.Snapshot is null)
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

        HasCurrentMatch = true;
        CanRetry = false;
        StateTitle = string.Empty;
        StateMessage = string.Empty;
        ConnectionTitle = _localization.Get("Current.Status.Connected");
        ConnectionDetails = _localization.Format(
            "Current.Status.ConnectedDetails",
            _localization.Plural("Count.Player", match.Players.Count));
        GameTime = FormatTime(match.GameTimeSeconds);

        if (activePlayer is not null)
        {
            ActiveChampion = activePlayer.ChampionName;
            ActiveChampionInitials = GetInitials(activePlayer.ChampionName);
            ActiveChampionImageUri = FindChampionImage(activePlayer.ChampionName);
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
            OpponentChampionImageUri = FindChampionImage(matchup.Opponent.ChampionName);
        }
        else
        {
            OpponentChampion = "—";
            OpponentChampionInitials = "—";
            OpponentChampionImageUri = null;
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
            TempoCue.Coaching.PowerState.Dangerous => _localization.Get("Power.Dangerous"),
            TempoCue.Coaching.PowerState.Cautious => _localization.Get("Power.Cautious"),
            TempoCue.Coaching.PowerState.Playable => _localization.Get("Power.Playable"),
            TempoCue.Coaching.PowerState.Favorable => _localization.Get("Power.Favorable"),
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
        var riotId = player.RiotId?.DisplayName ?? string.Empty;
        var profileUri = LeagueOfGraphsProfileUriBuilder.Build(
            player.RiotId,
            _leagueClientRegion);
        var profileLinkToolTip = profileUri is null
            ? string.Empty
            : _localization.Format("Current.Participant.ProfileLinkHint", riotId);

        return new ParticipantViewModel(
            GetPositionCode(player.Position),
            GetInitials(player.ChampionName),
            FindChampionImage(player.ChampionName),
            player.ChampionName,
            riotId,
            profileUri,
            profileLinkToolTip,
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
        HasCurrentMatch = false;
        CanRetry = Status is MatchSnapshotReadStatus.Timeout
            or MatchSnapshotReadStatus.InvalidData
            or MatchSnapshotReadStatus.Failed;
        ConnectionTitle = Status switch
        {
            MatchSnapshotReadStatus.GameLoading => _localization.Get("Current.Status.Loading"),
            MatchSnapshotReadStatus.Timeout => _localization.Get("Current.Status.Timeout"),
            MatchSnapshotReadStatus.InvalidData => _localization.Get("Current.Status.InvalidData"),
            MatchSnapshotReadStatus.Failed => _localization.Get("Current.Status.Error"),
            _ => _localization.Get("Current.Status.Waiting")
        };
        ConnectionDetails = Status switch
        {
            MatchSnapshotReadStatus.GameLoading =>
                _localization.Get("Current.Status.LoadingDetails"),
            MatchSnapshotReadStatus.Timeout =>
                _localization.Get("Current.Status.TimeoutDetails"),
            MatchSnapshotReadStatus.InvalidData =>
                _localization.Get("Current.Status.InvalidDataDetails"),
            MatchSnapshotReadStatus.Failed =>
                _localization.Get("Current.Status.ErrorDetails"),
            _ => _localization.Get("Current.Status.WaitingDetails")
        };
        StateTitle = _localization.Get(GetStateResourceKey("Title"));
        StateMessage = _localization.Get(GetStateResourceKey("Message"));
        GameTime = "—";
        ActiveChampion = "—";
        ActiveChampionInitials = "—";
        ActiveChampionImageUri = null;
        ActiveSummary = _localization.Get("Current.Player.Waiting");
        ActivePosition = "—";
        ActiveKda = "—";
        ActiveCreepScore = "—";
        OpponentChampion = "—";
        OpponentChampionInitials = "—";
        OpponentChampionImageUri = null;
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

    private string GetStateResourceKey(string suffix)
    {
        var state = Status switch
        {
            MatchSnapshotReadStatus.GameLoading => "Loading",
            MatchSnapshotReadStatus.Timeout => "Timeout",
            MatchSnapshotReadStatus.InvalidData => "InvalidData",
            MatchSnapshotReadStatus.Failed => "Error",
            _ => "Waiting"
        };

        return $"Current.State.{state}.{suffix}";
    }

    private void Localization_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]")
        {
            return;
        }

        if (_lastUpdate is
            {
                Status: MatchSnapshotReadStatus.Success,
                Snapshot: { } snapshot
            })
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
        ActiveChampionImageUri = null;
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

    private Uri? FindChampionImage(string championName) =>
        _championImages.GetValueOrDefault(NormalizeChampionKey(championName));

    private static IReadOnlyDictionary<string, Uri> BuildChampionImageLookup(
        IReadOnlyList<ChampionCatalogEntry> champions)
    {
        var images = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        foreach (var champion in champions)
        {
            images.TryAdd(NormalizeChampionKey(champion.Id), champion.ImageUri);
            images.TryAdd(NormalizeChampionKey(champion.DisplayName), champion.ImageUri);
        }

        return images;
    }

    private static string NormalizeChampionKey(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private void RerenderLastUpdate()
    {
        if (_lastUpdate is not
            {
                Status: MatchSnapshotReadStatus.Success,
                Snapshot: { } snapshot
            })
        {
            return;
        }

        RenderSnapshot(snapshot, _lastUpdate.CoachPackSelection);
        StateUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static void TryCancel(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Завершившийся запрос мог освободить свой CTS одновременно с новой загрузкой.
        }
    }

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
}
