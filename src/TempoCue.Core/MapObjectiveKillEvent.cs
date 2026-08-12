namespace TempoCue.Core;

/// <summary>Нормализованный факт убийства нейтрального объекта в активном матче.</summary>
public sealed record MapObjectiveKillEvent(
    int EventId,
    MapObjectiveKind Objective,
    TimeSpan GameTime);
