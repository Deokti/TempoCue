namespace TempoCue.Application;

/// <summary>Объединённый план всех включённых наборов, подходящих активному игроку.</summary>
public sealed record ActiveMapControlPlan(
    IReadOnlyList<MapControlScheduledAdvice> ScheduledAdvice,
    IReadOnlyList<string> ActivePackNames,
    IReadOnlyList<MapControlPersistentAdvice>? PersistentAdvice = null)
{
    public IReadOnlyList<MapControlPersistentAdvice> AvailablePersistentAdvice =>
        PersistentAdvice ?? [];

    public static ActiveMapControlPlan Empty { get; } = new([], [], []);
}
