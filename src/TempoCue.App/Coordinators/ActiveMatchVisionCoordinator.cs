using System.ComponentModel;
using System.Diagnostics;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.Coordinators;

/// <summary>
/// Связывает участников активного матча с нейтральными эталонами портретов Vision.
/// </summary>
internal sealed class ActiveMatchVisionCoordinator :
    IActiveMatchVisionDiagnosticsSource,
    IDisposable
{
    private static readonly TimeSpan ReferenceLoadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DefaultObservationTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultReferenceRetryInterval = TimeSpan.FromSeconds(30);

    private readonly IActiveMatchMonitor _monitor;
    private readonly IChampionCatalog _championCatalog;
    private readonly IChampionPortraitReferenceLoader _portraitLoader;
    private readonly IMinimapChampionReferenceSink _referenceSink;
    private readonly IMinimapChampionObservationSource? _observationSource;
    private readonly LocalizationService _localization;
    private readonly object _sync = new();
    private readonly ParticipantVisibilityTracker _visibilityTracker = new();
    private readonly TimeSpan _observationTimeout;
    private readonly TimeSpan _referenceRetryInterval;
    private DateTimeOffset? _lastObservationReceivedAt;
    private DateTimeOffset? _referencesFailedAt;

    private CancellationTokenSource? _loadCancellation;
    private ActiveMatchVisionDiagnosticsSnapshot _diagnosticsSnapshot =
        ActiveMatchVisionDiagnosticsSnapshot.Empty;
    private IReadOnlyDictionary<string, ParticipantReferenceResolution> _resolutions =
        new Dictionary<string, ParticipantReferenceResolution>(
            StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, ParticipantVisibilitySnapshot> _visibilityStates =
        new Dictionary<string, ParticipantVisibilitySnapshot>(
            StringComparer.OrdinalIgnoreCase);
    private ParticipantContext? _currentContext;
    private string? _participantSignature;
    private int _generation;
    private bool _isDisposed;

    public ActiveMatchVisionCoordinator(
        IActiveMatchMonitor monitor,
        IChampionCatalog championCatalog,
        IChampionPortraitReferenceLoader portraitLoader,
        IMinimapChampionReferenceSink referenceSink,
        LocalizationService localization,
        IMinimapChampionObservationSource? observationSource = null,
        TimeSpan? observationTimeout = null,
        TimeSpan? referenceRetryInterval = null)
    {
        _observationTimeout = observationTimeout ?? DefaultObservationTimeout;
        _referenceRetryInterval = referenceRetryInterval ?? DefaultReferenceRetryInterval;
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _championCatalog = championCatalog ?? throw new ArgumentNullException(nameof(championCatalog));
        _portraitLoader = portraitLoader ?? throw new ArgumentNullException(nameof(portraitLoader));
        _referenceSink = referenceSink ?? throw new ArgumentNullException(nameof(referenceSink));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _observationSource = observationSource;

        _monitor.Updated += OnMatchUpdated;
        _localization.PropertyChanged += OnLocalizationChanged;
        if (_observationSource is not null)
        {
            _observationSource.ObservationAvailable += OnObservationAvailable;
        }
        Schedule(_monitor.CurrentUpdate);
    }

    public ActiveMatchVisionDiagnosticsSnapshot DiagnosticsSnapshot =>
        Volatile.Read(ref _diagnosticsSnapshot);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _generation++;
            _loadCancellation?.Cancel();
            _loadCancellation = null;
            _visibilityTracker.Reset();
            _visibilityStates = new Dictionary<string, ParticipantVisibilitySnapshot>(
                StringComparer.OrdinalIgnoreCase);
            _currentContext = null;
        }

        _monitor.Updated -= OnMatchUpdated;
        _localization.PropertyChanged -= OnLocalizationChanged;
        if (_observationSource is not null)
        {
            _observationSource.ObservationAvailable -= OnObservationAvailable;
        }
    }

    private void OnMatchUpdated(object? sender, ActiveMatchUpdateEventArgs e) =>
        Schedule(e.Update);

    private void OnObservationAvailable(
        object? sender,
        MinimapChampionObservationEventArgs e)
    {
        lock (_sync)
        {
            if (_isDisposed || _currentContext is not { } context)
            {
                return;
            }

            _lastObservationReceivedAt = DateTimeOffset.UtcNow;
            var trackedPlayers = GetTrackedPlayersLocked(context);
            var observations = e.Candidates
                .Select(candidate => CreateObservation(
                    candidate,
                    trackedPlayers,
                    e.ImageWidth,
                    e.ImageHeight))
                .OfType<ParticipantVisionObservation>()
                .ToArray();
            var states = _visibilityTracker.Update(
                e.CapturedAt,
                trackedPlayers.Select(static player => player.ParticipantId).ToArray(),
                observations);
            _visibilityStates = states.ToDictionary(
                static state => state.ParticipantId,
                StringComparer.OrdinalIgnoreCase);
            Volatile.Write(
                ref _diagnosticsSnapshot,
                context.CreateDiagnosticsSnapshot(_resolutions, _visibilityStates));
        }
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocalizationService.CurrentLanguageCode))
        {
            lock (_sync)
            {
                _participantSignature = null;
            }

            Schedule(_monitor.CurrentUpdate);
        }
    }

    private void Schedule(ActiveMatchUpdate? update)
    {
        var context = CreateContext(update);
        CancellationTokenSource? cancellation;
        int generation;
        var isReferenceRetry = false;
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            if (context is not null &&
                string.Equals(
                    context.Signature,
                    _participantSignature,
                    StringComparison.Ordinal))
            {
                _currentContext = context;
                DegradeVisibilityWhenStreamIsStaleLocked(context);
                Volatile.Write(
                    ref _diagnosticsSnapshot,
                    context.CreateDiagnosticsSnapshot(
                        _resolutions,
                        _visibilityStates,
                        ObservationsAreStaleLocked()));
                if (_referencesFailedAt is not { } failedAt ||
                    DateTimeOffset.UtcNow - failedAt < _referenceRetryInterval)
                {
                    return;
                }

                // Временный сбой сети в начале матча не должен выключать
                // распознавание до смены состава: подготовка повторяется
                // на очередном обновлении монитора с ограниченной частотой.
                _referencesFailedAt = null;
                _generation++;
                generation = _generation;
                _loadCancellation?.Cancel();
                _loadCancellation = new CancellationTokenSource();
                cancellation = _loadCancellation;
                isReferenceRetry = true;
            }
            else
            {
                _participantSignature = context?.Signature;
                _currentContext = context;
                _resolutions = new Dictionary<string, ParticipantReferenceResolution>(
                    StringComparer.OrdinalIgnoreCase);
                _visibilityTracker.Reset();
                _visibilityStates = new Dictionary<string, ParticipantVisibilitySnapshot>(
                    StringComparer.OrdinalIgnoreCase);
                _lastObservationReceivedAt = null;
                _referencesFailedAt = null;
                _generation++;
                generation = _generation;
                _loadCancellation?.Cancel();
                _loadCancellation = context is null ? null : new CancellationTokenSource();
                cancellation = _loadCancellation;
            }
        }

        if (isReferenceRetry)
        {
            _ = LoadAndPublishAsync(context!, generation, cancellation!.Token);
            return;
        }

        if (context is null)
        {
            Volatile.Write(
                ref _diagnosticsSnapshot,
                ActiveMatchVisionDiagnosticsSnapshot.Empty);
            _referenceSink.SetChampionReferences([]);
            return;
        }

        Volatile.Write(ref _diagnosticsSnapshot, context.CreateDiagnosticsSnapshot());
        // Эталоны прошлого матча нельзя применять к новому составу, пока его портреты
        // ещё загружаются: это дало бы уверенное, но заведомо неверное распознавание.
        _referenceSink.SetChampionReferences([]);

        _ = LoadAndPublishAsync(context, generation, cancellation!.Token);
    }

    private async Task LoadAndPublishAsync(
        ParticipantContext context,
        int generation,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(ReferenceLoadTimeout);
        var loadToken = timeoutCancellation.Token;

        try
        {
            var catalog = await _championCatalog.LoadAsync(
                context.LanguageCode,
                loadToken).ConfigureAwait(false);
            if (!catalog.IsAvailable)
            {
                MarkReferenceLoadFailed(generation);
                PublishIfCurrent(
                    generation,
                    [],
                    context,
                    CreateUnavailableResolutions(context));
                return;
            }

            var champions = BuildChampionLookup(catalog.Champions);
            var loadingTasks = context.Players
                .Select(player => LoadPlayerReferenceAsync(
                    player,
                    champions,
                    loadToken))
                .ToArray();
            var loadedParticipants = await Task.WhenAll(loadingTasks).ConfigureAwait(false);
            var references = loadedParticipants
                .Where(static participant => participant?.Reference is not null)
                .Select(static participant => participant!.Reference!)
                .GroupBy(
                    static reference => (reference.ChampionId, reference.Affiliation))
                .Select(static group => group.First())
                .ToArray();
            var resolutions = loadedParticipants
                .GroupBy(
                    static participant => participant.ReferenceKey,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    static group => group.Key,
                    static group =>
                    {
                        var participant = group.First();
                        return new ParticipantReferenceResolution(
                            participant.ChampionId,
                            participant.Reference is null
                                ? ChampionPortraitReferenceStatus.Unavailable
                                : ChampionPortraitReferenceStatus.Ready);
                    },
                    StringComparer.OrdinalIgnoreCase);
            PublishIfCurrent(generation, references, context, resolutions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException)
        {
            Trace.TraceError(
                "Подготовка портретов участников превысила лимит {0} секунд.",
                ReferenceLoadTimeout.TotalSeconds);
            MarkReferenceLoadFailed(generation);
            PublishIfCurrent(
                generation,
                [],
                context,
                CreateUnavailableResolutions(context));
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "Не удалось подготовить портреты участников для миникарты ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            MarkReferenceLoadFailed(generation);
            PublishIfCurrent(
                generation,
                [],
                context,
                CreateUnavailableResolutions(context));
        }
    }

    /// <summary>
    /// Отдельный отсутствующий в каталоге чемпион не считается сбоем: повторяются
    /// только полные неудачи подготовки — недоступный каталог, таймаут и исключение.
    /// </summary>
    private void MarkReferenceLoadFailed(int generation)
    {
        lock (_sync)
        {
            if (!_isDisposed && generation == _generation)
            {
                _referencesFailedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private async Task<LoadedParticipantReference> LoadPlayerReferenceAsync(
        ParticipantReference player,
        IReadOnlyDictionary<string, ChampionCatalogEntry> champions,
        CancellationToken cancellationToken)
    {
        if (!champions.TryGetValue(NormalizeChampionKey(player.ChampionName), out var champion))
        {
            Trace.TraceWarning(
                "В каталоге Data Dragon не найден чемпион {0}.",
                player.ChampionName);
            return new LoadedParticipantReference(player.ReferenceKey, null, null);
        }

        var reference = await _portraitLoader.LoadAsync(
            champion,
            player.Affiliation,
            cancellationToken).ConfigureAwait(false);
        return new LoadedParticipantReference(
            player.ReferenceKey,
            champion.Id,
            reference);
    }

    private void PublishIfCurrent(
        int generation,
        IReadOnlyList<MinimapChampionReference> references,
        ParticipantContext context,
        IReadOnlyDictionary<string, ParticipantReferenceResolution> resolutions)
    {
        lock (_sync)
        {
            if (_isDisposed || generation != _generation)
            {
                return;
            }

            _resolutions = resolutions;
            // Запись снимка выполняется под общим замком: параллельное наблюдение кадра
            // не должно быть перезаписано более старым результатом загрузки портретов.
            Volatile.Write(
                ref _diagnosticsSnapshot,
                (_currentContext ?? context).CreateDiagnosticsSnapshot(
                    resolutions,
                    _visibilityStates,
                    ObservationsAreStaleLocked()));
        }

        _referenceSink.SetChampionReferences(references);
    }

    private static IReadOnlyDictionary<string, ParticipantReferenceResolution>
        CreateUnavailableResolutions(ParticipantContext context) =>
        context.Players
            .Select(static player => player.ReferenceKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
            static referenceKey => referenceKey,
            static _ => new ParticipantReferenceResolution(
                    null,
                    ChampionPortraitReferenceStatus.Unavailable),
            StringComparer.OrdinalIgnoreCase);

    private ParticipantContext? CreateContext(ActiveMatchUpdate? update)
    {
        if (update is not
            {
                Status: MatchSnapshotReadStatus.Success,
                Snapshot.Match: { } match
            })
        {
            return null;
        }

        var activePlayer = match.GetActivePlayer();
        if (activePlayer is null || activePlayer.Team == TeamSide.Unknown)
        {
            return null;
        }

        var players = match.Players
            .Where(static player =>
                player.Team != TeamSide.Unknown &&
                !string.IsNullOrWhiteSpace(player.ChampionName))
            .Select(player => new ParticipantReference(
                player.ParticipantId,
                player.ChampionName,
                player.Position,
                player.Team,
                player.IsDead,
                player.Team == activePlayer.Team
                    ? MinimapMarkerAffiliation.Ally
                    : MinimapMarkerAffiliation.Enemy))
            .ToArray();
        if (players.Length == 0)
        {
            return null;
        }

        var languageCode = _localization.CurrentLanguageCode;
        var signature = string.Join(
            '|',
            players
                .Select(static player => player.ReferenceKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase));
        return new ParticipantContext(
            $"{languageCode}|{activePlayer.Team}|{signature}",
            languageCode,
            activePlayer.Team,
            players);
    }

    private ParticipantReference[] GetTrackedPlayersLocked(ParticipantContext context) =>
        context.Players
            .Where(player =>
                !player.IsDead &&
                _resolutions.TryGetValue(player.ReferenceKey, out var resolution) &&
                resolution.Status == ChampionPortraitReferenceStatus.Ready &&
                !string.IsNullOrWhiteSpace(resolution.ChampionId))
            .ToArray();

    private bool ObservationsAreStaleLocked() =>
        _lastObservationReceivedAt is not { } receivedAt ||
        DateTimeOffset.UtcNow - receivedAt >= _observationTimeout;

    /// <summary>
    /// Прерванный поток кадров не должен бессрочно удерживать «Виден»: синтетический
    /// пропуск проводит состояния через обычный период удержания трекера, поэтому
    /// возобновлённый поток требует полного повторного подтверждения.
    /// </summary>
    private void DegradeVisibilityWhenStreamIsStaleLocked(ParticipantContext context)
    {
        if (!ObservationsAreStaleLocked() || _visibilityStates.Count == 0)
        {
            return;
        }

        var trackedIds = GetTrackedPlayersLocked(context)
            .Select(static player => player.ParticipantId)
            .ToArray();
        var states = _visibilityTracker.Update(DateTimeOffset.UtcNow, trackedIds, []);
        _visibilityStates = states.ToDictionary(
            static state => state.ParticipantId,
            StringComparer.OrdinalIgnoreCase);
    }

    private ParticipantVisionObservation? CreateObservation(
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<ParticipantReference> trackedPlayers,
        int imageWidth,
        int imageHeight)
    {
        if (string.IsNullOrWhiteSpace(candidate.ChampionId))
        {
            return null;
        }

        var player = trackedPlayers.FirstOrDefault(player =>
            player.Affiliation == candidate.Affiliation &&
            _resolutions.TryGetValue(player.ReferenceKey, out var resolution) &&
            string.Equals(
                resolution.ChampionId,
                candidate.ChampionId,
                StringComparison.OrdinalIgnoreCase));
        if (player is null)
        {
            return null;
        }

        var centerX = candidate.X + (candidate.Width / 2d);
        var centerY = candidate.Y + (candidate.Height / 2d);
        return new ParticipantVisionObservation(
            player.ParticipantId,
            Math.Clamp(candidate.IdentityConfidence, 0, 1),
            Math.Clamp(centerX / imageWidth, 0, 1),
            Math.Clamp(centerY / imageHeight, 0, 1));
    }

    private static IReadOnlyDictionary<string, ChampionCatalogEntry> BuildChampionLookup(
        IReadOnlyList<ChampionCatalogEntry> champions)
    {
        var lookup = new Dictionary<string, ChampionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var champion in champions)
        {
            lookup.TryAdd(NormalizeChampionKey(champion.Id), champion);
            lookup.TryAdd(NormalizeChampionKey(champion.DisplayName), champion);
        }

        return lookup;
    }

    private static string NormalizeChampionKey(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private sealed record ParticipantContext(
        string Signature,
        string LanguageCode,
        TeamSide ActivePlayerTeam,
        IReadOnlyList<ParticipantReference> Players)
    {
        public ActiveMatchVisionDiagnosticsSnapshot CreateDiagnosticsSnapshot(
            IReadOnlyDictionary<string, ParticipantReferenceResolution>? resolutions = null,
            IReadOnlyDictionary<string, ParticipantVisibilitySnapshot>? visibilityStates = null,
            bool observationsAreStale = false) =>
            new(
                ActivePlayerTeam,
                Players.Select(player =>
                {
                    var resolution = resolutions is not null && resolutions.TryGetValue(
                        player.ReferenceKey,
                        out var resolved)
                            ? resolved
                            : null;
                    var visibility = visibilityStates is not null &&
                        visibilityStates.TryGetValue(player.ParticipantId, out var tracked)
                            ? tracked
                            : null;
                    // Без живого потока наблюдений «не виден» — это догадка: статус
                    // честно опускается до «нет данных», а последнее подтверждённое
                    // время и координаты сохраняются для диагностики.
                    var visibilityStatus = observationsAreStale || visibility is null
                        ? ParticipantVisibilityStatus.Unknown
                        : visibility.Status;
                    return new ActiveMatchVisionParticipantSnapshot(
                        player.ParticipantId,
                        player.ChampionName,
                        resolution?.ChampionId,
                        player.Position,
                        player.RawTeam,
                        player.Affiliation,
                        resolution?.Status ?? ChampionPortraitReferenceStatus.Pending,
                        player.IsDead,
                        visibilityStatus,
                        visibility?.LastSeenAt,
                        visibility?.Confidence ?? 0,
                        visibility?.MapX,
                        visibility?.MapY);
                }).ToArray());
    }

    private sealed record ParticipantReference(
        string ParticipantId,
        string ChampionName,
        PlayerPosition Position,
        TeamSide RawTeam,
        bool IsDead,
        MinimapMarkerAffiliation Affiliation)
    {
        public string ReferenceKey =>
            $"{NormalizeChampionKey(ChampionName)}:{Affiliation}";
    }

    private sealed record LoadedParticipantReference(
        string ReferenceKey,
        string? ChampionId,
        MinimapChampionReference? Reference);

    private sealed record ParticipantReferenceResolution(
        string? ChampionId,
        ChampionPortraitReferenceStatus Status);
}
