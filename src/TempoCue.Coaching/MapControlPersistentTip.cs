namespace TempoCue.Coaching;

/// <summary>Справочная подсказка, доступная игроку в подробностях набора.</summary>
public sealed record MapControlPersistentTip
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public MapControlPersistentTipScope Scope { get; init; } =
        new(MapControlPersistentTipScopeKind.WholeMatch);
}
