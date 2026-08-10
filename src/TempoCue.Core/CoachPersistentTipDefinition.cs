namespace TempoCue.Core;

/// <summary>Постоянная подсказка, необязательно ограниченная одной фазой.</summary>
public sealed record CoachPersistentTipDefinition
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public string? ApplicableRuleId { get; init; }

    public string? Source { get; init; }
}
