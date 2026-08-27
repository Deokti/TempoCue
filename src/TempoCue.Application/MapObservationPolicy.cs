using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>
/// Решает, нужно ли сейчас смотреть на миникарту.
/// </summary>
/// <remarks>
/// Наблюдение стоит дорого: захват окна, отбор кадров и распознавание идут
/// непрерывно всю игру. Платить эту цену имеет смысл только тогда, когда есть
/// кому слушать результат, — то есть когда включено хотя бы одно правило
/// наблюдения в наборах контроля карты.
///
/// Правила берутся из плана, а не из библиотеки: план уже учёл и выключенные
/// наборы, и выключенные правила внутри включённых наборов, и применимость к
/// роли и стороне игрока. Набор, не подходящий этому матчу, не сможет
/// уведомить ни разу, поэтому смотреть ради него не на что.
/// </remarks>
public static class MapObservationPolicy
{
    /// <summary>Нужно ли сейчас захватывать и разбирать миникарту.</summary>
    public static bool IsWanted(ActiveMatchUpdate? update) =>
        update is { Status: MatchSnapshotReadStatus.Success }
        && update.MapControlPlan?.AvailableVisionRules.Count > 0;

    /// <summary>
    /// Идёт матч, но наблюдать не для кого. Отличается от простого «не нужно»
    /// тем, что это состояние надо объяснить: игрок видит пустую диагностику
    /// захвата во время игры и вправе знать, что это его собственный выбор,
    /// а не поломка.
    /// </summary>
    public static bool IsSwitchedOffDuringMatch(ActiveMatchUpdate? update) =>
        update is { Status: MatchSnapshotReadStatus.Success } && !IsWanted(update);
}
