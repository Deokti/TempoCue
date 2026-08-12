namespace TempoCue.Core;

/// <summary>Проверенный набор таймингов для непрерывного диапазона игровых патчей.</summary>
public sealed record MapObjectiveTimingSet(
    string RequestedPatch,
    string EffectiveFromPatch,
    string VerifiedThroughPatch,
    bool IsVerifiedForRequestedPatch,
    IReadOnlyList<MapObjectiveTiming> Objectives)
{
    /// <summary>Показывает, что используются последние известные данные предыдущего патча.</summary>
    public bool IsUsingPreviousPatchData => !IsVerifiedForRequestedPatch;

    /// <summary>Возвращает тайминг указанного объекта.</summary>
    public MapObjectiveTiming Get(MapObjectiveKind objective) =>
        Objectives.Single(timing => timing.Objective == objective);
}
