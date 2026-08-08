namespace RiftCoach.Core;

public sealed record CoachPack
{
    public int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Patch { get; init; }

    public IReadOnlyList<CoachRuleDefinition> Rules { get; init; } = [];
}
