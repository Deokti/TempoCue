namespace TempoCue.Application;

/// <summary>
/// Превращает краткие наблюдения отдельных кадров в устойчивое состояние участников.
/// </summary>
public sealed class ParticipantVisibilityTracker
{
    public const int DefaultRequiredConsecutiveDetections = 2;
    public static readonly TimeSpan DefaultMaximumDetectionGap =
        TimeSpan.FromMilliseconds(600);
    public static readonly TimeSpan DefaultDisappearanceGracePeriod =
        TimeSpan.FromSeconds(1);

    private readonly int _requiredConsecutiveDetections;
    private readonly TimeSpan _maximumDetectionGap;
    private readonly TimeSpan _disappearanceGracePeriod;
    private readonly Dictionary<string, TrackedParticipant> _participants =
        new(StringComparer.OrdinalIgnoreCase);

    public ParticipantVisibilityTracker(
        int requiredConsecutiveDetections = DefaultRequiredConsecutiveDetections,
        TimeSpan? maximumDetectionGap = null,
        TimeSpan? disappearanceGracePeriod = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredConsecutiveDetections);
        _requiredConsecutiveDetections = requiredConsecutiveDetections;
        _maximumDetectionGap = maximumDetectionGap ?? DefaultMaximumDetectionGap;
        _disappearanceGracePeriod = disappearanceGracePeriod ??
            DefaultDisappearanceGracePeriod;
        if (_maximumDetectionGap <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDetectionGap));
        }

        if (_disappearanceGracePeriod < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(disappearanceGracePeriod));
        }
    }

    public IReadOnlyList<ParticipantVisibilitySnapshot> Update(
        DateTimeOffset capturedAt,
        IReadOnlyList<string> expectedParticipantIds,
        IReadOnlyList<ParticipantVisionObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(expectedParticipantIds);
        ArgumentNullException.ThrowIfNull(observations);

        var expected = expectedParticipantIds
            .Where(static participantId => !string.IsNullOrWhiteSpace(participantId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        RemoveUnexpectedParticipants(expected);

        var observationsByParticipant = observations
            .Where(observation => expected.Contains(
                observation.ParticipantId,
                StringComparer.OrdinalIgnoreCase))
            .GroupBy(
                static observation => observation.ParticipantId,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderByDescending(static observation => observation.Confidence)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var snapshots = new ParticipantVisibilitySnapshot[expected.Length];
        for (var index = 0; index < expected.Length; index++)
        {
            var participantId = expected[index];
            if (!_participants.TryGetValue(participantId, out var participant))
            {
                participant = new TrackedParticipant(participantId);
                _participants.Add(participantId, participant);
            }

            observationsByParticipant.TryGetValue(participantId, out var observation);
            snapshots[index] = participant.Update(
                capturedAt,
                observation,
                _requiredConsecutiveDetections,
                _maximumDetectionGap,
                _disappearanceGracePeriod);
        }

        return snapshots;
    }

    public void Reset() => _participants.Clear();

    private void RemoveUnexpectedParticipants(IReadOnlyList<string> expectedParticipantIds)
    {
        var staleIds = _participants.Keys
            .Where(participantId => !expectedParticipantIds.Contains(
                participantId,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();
        foreach (var staleId in staleIds)
        {
            _participants.Remove(staleId);
        }
    }

    private sealed class TrackedParticipant(string participantId)
    {
        private int _consecutiveDetections;
        private DateTimeOffset? _lastDetectionAt;
        private DateTimeOffset? _lastSeenAt;
        private bool _isVisible;
        private double _confidence;
        private double? _mapX;
        private double? _mapY;

        public ParticipantVisibilitySnapshot Update(
            DateTimeOffset capturedAt,
            ParticipantVisionObservation? observation,
            int requiredConsecutiveDetections,
            TimeSpan maximumDetectionGap,
            TimeSpan disappearanceGracePeriod)
        {
            if (observation is not null)
            {
                var followsPrevious = _lastDetectionAt is { } previous &&
                    capturedAt >= previous &&
                    capturedAt - previous <= maximumDetectionGap;
                _consecutiveDetections = followsPrevious
                    ? _consecutiveDetections + 1
                    : 1;
                _lastDetectionAt = capturedAt;
                _confidence = observation.Confidence;
                _mapX = observation.MapX;
                _mapY = observation.MapY;
                if (_isVisible ||
                    _consecutiveDetections >= requiredConsecutiveDetections)
                {
                    _isVisible = true;
                    _lastSeenAt = capturedAt;
                }
            }
            else
            {
                // Пропуск отдельного кадра не сбрасывает подтверждение: последовательность
                // обнаружений определяется только интервалом maximumDetectionGap, иначе
                // мигающее распознавание никогда не достигло бы двух подряд.
                if (_isVisible &&
                    _lastSeenAt is { } lastSeen &&
                    capturedAt >= lastSeen &&
                    capturedAt - lastSeen > disappearanceGracePeriod)
                {
                    _isVisible = false;
                }
            }

            var status = _isVisible
                ? ParticipantVisibilityStatus.Visible
                : observation is not null
                    ? ParticipantVisibilityStatus.Confirming
                    : ParticipantVisibilityStatus.NotVisible;
            return new ParticipantVisibilitySnapshot(
                participantId,
                status,
                _lastSeenAt,
                _confidence,
                _mapX,
                _mapY);
        }
    }
}
