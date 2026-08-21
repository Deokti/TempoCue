using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>
/// Преобразует нейтральное изменение видимости в безопасный игровой сигнал.
/// Неоднозначные команды, роли и состояние смерти намеренно не угадываются.
/// </summary>
public static class MapAwarenessSignalPolicy
{
    /// <summary>
    /// Минимальная итоговая уверенность наблюдения для игрового уведомления.
    /// Replay-корпус отделяет встреченные ложные Amumu на башнях и миньонах
    /// (не выше 0,67) от достаточно чистого эксплуатационного сигнала.
    /// Диагностика при этом продолжает показывать сомнительное распознавание.
    /// </summary>
    public const double MinimumOperationalConfidence =
        ParticipantVisibilityTracker.DefaultMinimumObservationConfidence;

    public static MapAwarenessSignal? Evaluate(
        ParticipantVisibilityTransition transition,
        MatchState match)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(match);

        if (transition.Confidence < MinimumOperationalConfidence)
        {
            return null;
        }

        var activePlayer = match.GetActivePlayer();
        if (activePlayer is null || activePlayer.Team == TeamSide.Unknown)
        {
            return null;
        }

        var participant = match.Players.FirstOrDefault(player =>
            string.Equals(
                player.ParticipantId,
                transition.ParticipantId,
                StringComparison.OrdinalIgnoreCase));
        if (participant is null
            || participant.Team == TeamSide.Unknown
            || participant.Team == activePlayer.Team
            || participant.IsDead)
        {
            return null;
        }

        var kind = (participant.Position, transition.Kind) switch
        {
            (PlayerPosition.Jungle, ParticipantVisibilityTransitionKind.Appeared) =>
                MapAwarenessSignalKind.EnemyJunglerAppeared,
            (PlayerPosition.Middle, ParticipantVisibilityTransitionKind.Disappeared) =>
                MapAwarenessSignalKind.EnemyMidDisappeared,
            _ => (MapAwarenessSignalKind?)null
        };

        return kind is { } signalKind
            ? new MapAwarenessSignal(
                signalKind,
                participant.ParticipantId,
                participant.ChampionName,
                transition.OccurredAt,
                transition.Confidence,
                transition.MapX,
                transition.MapY)
            : null;
    }
}
