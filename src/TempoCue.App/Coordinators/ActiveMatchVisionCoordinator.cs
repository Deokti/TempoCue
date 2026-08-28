using System.ComponentModel;
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
    IParticipantVisibilityTransitionSource,
    IDisposable
{
    private static readonly TimeSpan DefaultObservationTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultReferenceRetryInterval = TimeSpan.FromSeconds(30);

    private readonly IActiveMatchMonitor _monitor;
    private readonly ChampionReferenceBatchLoader _referenceLoader;
    private readonly IMinimapChampionReferenceSink _referenceSink;
    private readonly IMinimapChampionObservationSource? _observationSource;
    private readonly LocalizationService _localization;
    private readonly object _sync = new();
    private readonly ParticipantVisibilityTracker _visibilityTracker = new();
    private readonly ParticipantVisibilityTransitionDetector _transitionDetector = new();
    private readonly TimeSpan _observationTimeout;
    private readonly TimeSpan _referenceRetryInterval;
    private DateTimeOffset? _lastObservationReceivedAt;
    private DateTimeOffset? _referencesFailedAt;

    private CancellationTokenSource? _loadCancellation;
    private ActiveMatchVisionDiagnosticsSnapshot _diagnosticsSnapshot =
        ActiveMatchVisionDiagnosticsSnapshot.Empty;
    private IReadOnlyDictionary<string, ActiveMatchVisionReferenceResolution> _resolutions =
        new Dictionary<string, ActiveMatchVisionReferenceResolution>(
            StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, ParticipantVisibilitySnapshot> _visibilityStates =
        new Dictionary<string, ParticipantVisibilitySnapshot>(
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Игровое время последнего наблюдения каждого участника. Хранится рядом с
    /// часами, а не вместо них: часы нужны для устаревания потока кадров, а
    /// игроку важно игровое время — на паузе они расходятся.
    /// </summary>
    private readonly Dictionary<string, TimeSpan> _lastSeenGameTimes =
        new(StringComparer.OrdinalIgnoreCase);
    private ActiveMatchVisionParticipantContext? _currentContext;
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
        _referenceLoader = new ChampionReferenceBatchLoader(championCatalog, portraitLoader);
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

    public event EventHandler<ParticipantVisibilityTransition>? TransitionAvailable;

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
            _transitionDetector.Reset();
            _lastSeenGameTimes.Clear();
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

    /// <summary>
    /// Запоминает игровое время, когда участника видели в последний раз.
    /// </summary>
    /// <remarks>
    /// Трекер видимости работает по часам: ему нужно устаревание потока кадров,
    /// а не игровой таймер. Игроку же нужно именно игровое время — по нему он
    /// считает и возрождение, и подход соперника. На паузе часы и таймер
    /// расходятся, поэтому пересчитывать одно из другого нельзя.
    /// </remarks>
    private void RememberLastSeenGameTimes(
        IReadOnlyList<ParticipantVisibilitySnapshot> states)
    {
        if (_monitor.CurrentUpdate?.Snapshot?.Match is not { } match)
        {
            return;
        }

        var gameTime = TimeSpan.FromSeconds(Math.Max(0, match.GameTimeSeconds));
        foreach (var state in states)
        {
            if (state.Status == ParticipantVisibilityStatus.Visible)
            {
                _lastSeenGameTimes[state.ParticipantId] = gameTime;
            }
        }
    }

    private void OnMatchUpdated(object? sender, ActiveMatchUpdateEventArgs e) =>
        Schedule(e.Update);

    private void OnObservationAvailable(
        object? sender,
        MinimapChampionObservationEventArgs e)
    {
        IReadOnlyList<ParticipantVisibilityTransition> transitions;
        lock (_sync)
        {
            if (_isDisposed || _currentContext is not { } context)
            {
                return;
            }

            if (!e.IsSuccessful)
            {
                // Непрочитанный кадр — не доказательство отсутствия: состояние
                // участников сохраняется и не рождает переходов, а продолжительный
                // сбой чтения переводит статусы в «нет данных» тем же путём, что
                // и полное отсутствие кадров.
                DegradeVisibilityWhenStreamIsStaleLocked(context);
                Volatile.Write(
                    ref _diagnosticsSnapshot,
                    context.CreateDiagnosticsSnapshot(
                        _resolutions,
                        _visibilityStates,
                        ObservationsAreStaleLocked(),
                        _lastSeenGameTimes));
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
            RememberLastSeenGameTimes(states);
            transitions = _transitionDetector.Update(e.CapturedAt, states);
            Volatile.Write(
                ref _diagnosticsSnapshot,
                context.CreateDiagnosticsSnapshot(
                    _resolutions,
                    _visibilityStates,
                    observationsAreStale: false,
                    _lastSeenGameTimes));
        }

        foreach (var transition in transitions)
        {
            TransitionAvailable?.Invoke(this, transition);
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
        var context = ActiveMatchVisionParticipantContext.Create(
            update,
            _localization.CurrentLanguageCode);
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
                        ObservationsAreStaleLocked(),
                        _lastSeenGameTimes));
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
                _resolutions = new Dictionary<string, ActiveMatchVisionReferenceResolution>(
                    StringComparer.OrdinalIgnoreCase);
                _visibilityTracker.Reset();
                _transitionDetector.Reset();
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
        ActiveMatchVisionParticipantContext context,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _referenceLoader.LoadAsync(
                context,
                cancellationToken).ConfigureAwait(false);
            if (result.ShouldRetry)
            {
                MarkReferenceLoadFailed(generation);
            }

            PublishIfCurrent(generation, result.References, context, result.Resolutions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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

    private void PublishIfCurrent(
        int generation,
        IReadOnlyList<MinimapChampionReference> references,
        ActiveMatchVisionParticipantContext context,
        IReadOnlyDictionary<string, ActiveMatchVisionReferenceResolution> resolutions)
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
                    ObservationsAreStaleLocked(),
                    _lastSeenGameTimes));
        }

        _referenceSink.SetChampionReferences(references);
    }

    private ActiveMatchVisionParticipant[] GetTrackedPlayersLocked(
        ActiveMatchVisionParticipantContext context) =>
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
    private void DegradeVisibilityWhenStreamIsStaleLocked(
        ActiveMatchVisionParticipantContext context)
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
        // Якоря детектора снимаются вместе с деградацией: первое устойчивое
        // состояние после восстановления потока фиксируется без игрового события.
        _ = _transitionDetector.Update(
            DateTimeOffset.UtcNow,
            states.Select(static state => state with
            {
                Status = ParticipantVisibilityStatus.Unknown
            }).ToArray());
    }

    private ParticipantVisionObservation? CreateObservation(
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<ActiveMatchVisionParticipant> trackedPlayers,
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
            // Прежде сюда шла итоговая оценка — среднее сходства с портретом и
            // уверенности цветного контура. Усреднение вводилось, когда оно было
            // единственной защитой от башен и миньонов; отклонение по отрицательным
            // эталонам появилось позже и делает ту же работу явно.
            //
            // Цена усреднения оказалась велика: шумный контур топил отличное
            // совпадение портрета, и до игрока доходили 29 распознанных чемпионов
            // из 45. Крайний случай — Diana со сходством 0,946 и итоговой оценкой
            // 0,709. Теперь видимость решается по сходству с портретом, а качество
            // контура остаётся тем, чем и было: признаком, что здесь вообще есть
            // метка чемпиона.
            Math.Clamp(candidate.IdentityConfidence, 0, 1),
            Math.Clamp(centerX / imageWidth, 0, 1),
            Math.Clamp(centerY / imageHeight, 0, 1));
    }

}
