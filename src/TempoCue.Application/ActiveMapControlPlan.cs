namespace TempoCue.Application;

/// <summary>Объединённый план всех включённых наборов, подходящих активному игроку.</summary>
public sealed record ActiveMapControlPlan(
    IReadOnlyList<MapControlScheduledAdvice> ScheduledAdvice,
    IReadOnlyList<string> ActivePackNames)
{
    public static ActiveMapControlPlan Empty { get; } = new([], []);
}
