namespace TempoCue.Application;

/// <summary>
/// Сравнивает устойчивые снимки трекера и отделяет переходы от покадрового шума.
/// </summary>
public sealed class ParticipantVisibilityTransitionDetector
{
    private readonly Dictionary<string, ParticipantVisibilityStatus> _previousStatuses =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ParticipantVisibilityTransition> Update(
        DateTimeOffset capturedAt,
        IReadOnlyList<ParticipantVisibilitySnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var currentIds = snapshots
            .Select(static snapshot => snapshot.ParticipantId)
            .Where(static participantId => !string.IsNullOrWhiteSpace(participantId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var staleId in _previousStatuses.Keys
                     .Where(participantId => !currentIds.Contains(participantId))
                     .ToArray())
        {
            _previousStatuses.Remove(staleId);
        }

        var transitions = new List<ParticipantVisibilityTransition>();
        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.ParticipantId))
            {
                continue;
            }

            if (!_previousStatuses.TryGetValue(snapshot.ParticipantId, out var previousStatus))
            {
                _previousStatuses[snapshot.ParticipantId] = snapshot.Status;
                continue;
            }

            // После потери потока новый известный снимок становится базовой точкой.
            // Иначе восстановление захвата могло бы выглядеть как игровое событие.
            if (previousStatus == ParticipantVisibilityStatus.Unknown ||
                snapshot.Status == ParticipantVisibilityStatus.Unknown)
            {
                _previousStatuses[snapshot.ParticipantId] = snapshot.Status;
                continue;
            }

            ParticipantVisibilityTransitionKind? kind =
                previousStatus != ParticipantVisibilityStatus.Visible &&
                snapshot.Status == ParticipantVisibilityStatus.Visible
                    ? ParticipantVisibilityTransitionKind.Appeared
                    : previousStatus == ParticipantVisibilityStatus.Visible &&
                      snapshot.Status == ParticipantVisibilityStatus.NotVisible
                        ? ParticipantVisibilityTransitionKind.Disappeared
                        : null;
            if (kind is not null)
            {
                transitions.Add(new ParticipantVisibilityTransition(
                    snapshot.ParticipantId,
                    kind.Value,
                    capturedAt,
                    snapshot.LastSeenAt,
                    snapshot.Confidence,
                    snapshot.MapX,
                    snapshot.MapY));
            }

            _previousStatuses[snapshot.ParticipantId] = snapshot.Status;
        }

        return transitions;
    }

    public void Reset() => _previousStatuses.Clear();
}
