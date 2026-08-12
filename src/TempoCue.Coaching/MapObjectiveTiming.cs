namespace TempoCue.Coaching;

/// <summary>Патч-зависимый график появления одного эпического объекта.</summary>
public sealed record MapObjectiveTiming(
    MapObjectiveKind Objective,
    TimeSpan InitialSpawnTime,
    TimeSpan? RespawnDelay)
{
    /// <summary>Показывает, может ли объект появиться повторно после убийства.</summary>
    public bool Respawns => RespawnDelay is not null;
}
