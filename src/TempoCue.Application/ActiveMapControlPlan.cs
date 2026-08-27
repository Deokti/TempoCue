namespace TempoCue.Application;

/// <summary>Объединённый план всех включённых наборов, подходящих активному игроку.</summary>
public sealed record ActiveMapControlPlan(
    IReadOnlyList<MapControlScheduledAdvice> ScheduledAdvice,
    IReadOnlyList<string> ActivePackNames,
    IReadOnlyList<MapControlPersistentAdvice>? PersistentAdvice = null,
    IReadOnlyList<MapVisionRule>? VisionRules = null)
{
    public IReadOnlyList<MapControlPersistentAdvice> AvailablePersistentAdvice =>
        PersistentAdvice ?? [];

    /// <summary>
    /// Правила уведомлений по наблюдению из включённых наборов. В плане они
    /// оказываются потому же, почему и остальное: набор решает, о чём
    /// напоминать, а приложение только выполняет. ADR 0016.
    /// </summary>
    public IReadOnlyList<MapVisionRule> AvailableVisionRules => VisionRules ?? [];

    public static ActiveMapControlPlan Empty { get; } = new([], [], [], []);
}
