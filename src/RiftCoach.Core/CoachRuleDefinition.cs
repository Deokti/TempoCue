namespace RiftCoach.Core;

public sealed record CoachRuleDefinition
{
    public required string Id { get; init; }

    public required string PlayerChampion { get; init; }

    public required string OpponentChampion { get; init; }

    public int MinimumLevel { get; init; } = 1;

    public int? MaximumLevel { get; init; }

    public IReadOnlyList<int> RequiredItemIds { get; init; } = [];

    public IReadOnlyList<int> ExcludedItemIds { get; init; } = [];

    public PowerState PowerState { get; init; }

    public int Priority { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public required string Source { get; init; }
}
