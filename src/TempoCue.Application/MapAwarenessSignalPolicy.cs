using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>
/// Преобразует нейтральное изменение видимости в игровой сигнал по правилам
/// включённых наборов контроля карты.
/// </summary>
/// <remarks>
/// Раньше набор сигналов был зашит здесь: появление вражеского лесника и пропажа
/// мидера, два случая в <c>switch</c>. Теперь приложение не знает наперёд, какие
/// сигналы существуют, — это решают наборы. ADR 0016.
///
/// Что осталось в коде намеренно: безопасность. Неоднозначные команды, роли и
/// состояние смерти не угадываются, а порог уверенности наблюдения — свойство
/// распознавания, а не пожелание автора набора. Отдать его в JSON значило бы
/// позволить набору включить заведомо ненадёжные наблюдения.
/// </remarks>
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
        MatchState match,
        IReadOnlyList<MapVisionRule> rules)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count == 0 || transition.Confidence < MinimumOperationalConfidence)
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
            || participant.IsDead
            || participant.Position == PlayerPosition.Unknown)
        {
            return null;
        }

        if (ToTransition(transition.Kind) is not { } visionTransition)
        {
            return null;
        }

        var rule = FindRule(rules, participant.Position, visionTransition);

        return rule is null
            ? null
            : new MapAwarenessSignal(
                rule,
                participant.ParticipantId,
                participant.ChampionName,
                transition.OccurredAt,
                transition.Confidence,
                transition.MapX,
                transition.MapY);
    }

    /// <summary>
    /// Первое подошедшее правило в авторском порядке.
    /// </summary>
    /// <remarks>
    /// Два набора могут описать одно и то же событие, и показывать два
    /// уведомления об одном наблюдении незачем. Поэтому побеждает первое, а
    /// остальные молчат — проверка набора называет такие пары ошибкой.
    ///
    /// Отдельный метод существует ради предпросмотра в редакторе: он обязан
    /// отвечать ровно то же, что случится в бою, а два одинаковых сопоставления
    /// в разных местах рано или поздно разойдутся.
    /// </remarks>
    public static MapVisionRule? FindRule(
        IReadOnlyList<MapVisionRule> rules,
        PlayerPosition position,
        MapVisionTransition transition)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.FirstOrDefault(candidate =>
            candidate.IsEnabled
            && candidate.Position == position
            && candidate.Transition == transition);
    }

    private static MapVisionTransition? ToTransition(
        ParticipantVisibilityTransitionKind kind) => kind switch
        {
            ParticipantVisibilityTransitionKind.Appeared => MapVisionTransition.Appeared,
            ParticipantVisibilityTransitionKind.Disappeared => MapVisionTransition.Disappeared,
            _ => null
        };
}
