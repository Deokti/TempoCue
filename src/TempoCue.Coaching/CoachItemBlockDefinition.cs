namespace TempoCue.Coaching;

/// <summary>Произвольная авторская группа предметов для будущего экспорта в League.</summary>
public sealed record CoachItemBlockDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? WhenToChoose { get; init; }

    public IReadOnlyList<int> ItemIds { get; init; } = [];
}
