namespace TempoCue.Application;

/// <summary>
/// Сравнивает устойчивые снимки трекера и отделяет переходы от покадрового шума.
/// Появление публикуется сразу после подтверждённой видимости, а исчезновение
/// требует отрицательного подтверждения: нескольких успешно обработанных кадров
/// без участника и минимальной длительности отсутствия. Кадры с ошибкой чтения
/// до детектора не доходят, поэтому каждый вызов Update — успешный кадр.
/// </summary>
public sealed class ParticipantVisibilityTransitionDetector
{
    /// <summary>
    /// Стартовые значения — проверяемая политика, ожидающая уточнения по записанным
    /// последовательностям реальных матчей; структура подтверждения гарантирует,
    /// что один-два кратких провала распознавания не создают событие.
    /// </summary>
    public const int DefaultRequiredAbsentFrames = 3;
    public static readonly TimeSpan DefaultMinimumAbsenceDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Радиус нормализованных координат, внутри которого видимый сосед считается
    /// возможной причиной пропажи портрета. Он намеренно равен дистанции, на
    /// которой распознаватель схлопывает близкие области (10 px канонического
    /// кадра ≈ 0,031, с запасом на округление): только там пропажа рамки
    /// действительно неотличима от слияния значков. Больший радиус подавлял бы
    /// настоящий уход мидера, пока свой мидер остаётся на линии рядом.
    /// </summary>
    public const double DefaultOcclusionSuppressionRadius = 0.04;

    private readonly int _requiredAbsentFrames;
    private readonly TimeSpan _minimumAbsenceDuration;
    private readonly double _occlusionSuppressionRadius;
    private readonly Dictionary<string, TrackedTransitionState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public ParticipantVisibilityTransitionDetector(
        int requiredAbsentFrames = DefaultRequiredAbsentFrames,
        TimeSpan? minimumAbsenceDuration = null,
        double occlusionSuppressionRadius = DefaultOcclusionSuppressionRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredAbsentFrames);
        _requiredAbsentFrames = requiredAbsentFrames;
        _minimumAbsenceDuration = minimumAbsenceDuration ?? DefaultMinimumAbsenceDuration;
        if (_minimumAbsenceDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAbsenceDuration));
        }

        if (!double.IsFinite(occlusionSuppressionRadius) || occlusionSuppressionRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occlusionSuppressionRadius));
        }

        _occlusionSuppressionRadius = occlusionSuppressionRadius;
    }

    public IReadOnlyList<ParticipantVisibilityTransition> Update(
        DateTimeOffset capturedAt,
        IReadOnlyList<ParticipantVisibilitySnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var currentIds = snapshots
            .Select(static snapshot => snapshot.ParticipantId)
            .Where(static participantId => !string.IsNullOrWhiteSpace(participantId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var staleId in _states.Keys
                     .Where(participantId => !currentIds.Contains(participantId))
                     .ToArray())
        {
            _states.Remove(staleId);
        }

        var transitions = new List<ParticipantVisibilityTransition>();
        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.ParticipantId))
            {
                continue;
            }

            if (!_states.TryGetValue(snapshot.ParticipantId, out var state))
            {
                state = new TrackedTransitionState();
                _states.Add(snapshot.ParticipantId, state);
            }

            switch (snapshot.Status)
            {
                case ParticipantVisibilityStatus.Unknown:
                    // Прерванный поток не даёт доказательств ни присутствия, ни
                    // отсутствия: якорь снимается, и восстановление захвата
                    // заново фиксирует состояние без игрового события.
                    state.Anchor = null;
                    state.AbsentFrames = 0;
                    break;

                case ParticipantVisibilityStatus.Confirming:
                    // Наблюдение в этом кадре — доказательство присутствия:
                    // ожидание исчезновения отменяется, а незаякоренный участник
                    // фиксируется как «ещё не виден» до полного подтверждения.
                    state.Anchor ??= ParticipantVisibilityStatus.NotVisible;
                    state.AbsentFrames = 0;
                    break;

                case ParticipantVisibilityStatus.Visible:
                    if (state.Anchor == ParticipantVisibilityStatus.NotVisible)
                    {
                        transitions.Add(CreateTransition(
                            snapshot,
                            ParticipantVisibilityTransitionKind.Appeared,
                            capturedAt));
                    }

                    state.Anchor = ParticipantVisibilityStatus.Visible;
                    state.AbsentFrames = 0;
                    break;

                case ParticipantVisibilityStatus.NotVisible:
                    if (state.Anchor is null)
                    {
                        state.Anchor = ParticipantVisibilityStatus.NotVisible;
                        break;
                    }

                    if (state.Anchor != ParticipantVisibilityStatus.Visible)
                    {
                        break;
                    }

                    state.AbsentFrames++;
                    if (state.AbsentFrames < _requiredAbsentFrames ||
                        !AbsenceDurationReached(snapshot, capturedAt))
                    {
                        break;
                    }

                    // Видимый сосед у последней позиции — возможная окклюзия:
                    // близкие портреты схлопываются в одну область, и пропажа
                    // рамки не доказывает уход. Подтверждение откладывается,
                    // пока сосед не отойдёт либо участник не вернётся.
                    if (HasVisibleNeighborNearLastPosition(snapshot, snapshots))
                    {
                        break;
                    }

                    transitions.Add(CreateTransition(
                        snapshot,
                        ParticipantVisibilityTransitionKind.Disappeared,
                        capturedAt));
                    state.Anchor = ParticipantVisibilityStatus.NotVisible;
                    state.AbsentFrames = 0;
                    break;
            }
        }

        return transitions;
    }

    public void Reset() => _states.Clear();

    private bool AbsenceDurationReached(
        ParticipantVisibilitySnapshot snapshot,
        DateTimeOffset capturedAt) =>
        snapshot.LastSeenAt is not { } lastSeen ||
        capturedAt < lastSeen ||
        capturedAt - lastSeen >= _minimumAbsenceDuration;

    private bool HasVisibleNeighborNearLastPosition(
        ParticipantVisibilitySnapshot snapshot,
        IReadOnlyList<ParticipantVisibilitySnapshot> snapshots)
    {
        if (snapshot.MapX is not { } mapX || snapshot.MapY is not { } mapY)
        {
            return false;
        }

        foreach (var other in snapshots)
        {
            if (other.Status != ParticipantVisibilityStatus.Visible ||
                string.Equals(
                    other.ParticipantId,
                    snapshot.ParticipantId,
                    StringComparison.OrdinalIgnoreCase) ||
                other.MapX is not { } otherX ||
                other.MapY is not { } otherY)
            {
                continue;
            }

            var deltaX = otherX - mapX;
            var deltaY = otherY - mapY;
            if ((deltaX * deltaX) + (deltaY * deltaY) <=
                _occlusionSuppressionRadius * _occlusionSuppressionRadius)
            {
                return true;
            }
        }

        return false;
    }

    private static ParticipantVisibilityTransition CreateTransition(
        ParticipantVisibilitySnapshot snapshot,
        ParticipantVisibilityTransitionKind kind,
        DateTimeOffset capturedAt) =>
        new(
            snapshot.ParticipantId,
            kind,
            capturedAt,
            snapshot.LastSeenAt,
            snapshot.Confidence,
            snapshot.MapX,
            snapshot.MapY);

    private sealed class TrackedTransitionState
    {
        /// <summary>Последнее объявленное состояние; null — поток был недостоверен.</summary>
        public ParticipantVisibilityStatus? Anchor { get; set; }

        /// <summary>Успешно обработанные кадры подряд без участника после якоря Visible.</summary>
        public int AbsentFrames { get; set; }
    }
}
