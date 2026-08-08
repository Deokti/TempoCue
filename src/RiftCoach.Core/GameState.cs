namespace RiftCoach.Core;

public sealed record GameState
{
    public required string PlayerChampion { get; init; }

    public required string OpponentChampion { get; init; }

    public required int PlayerLevel { get; init; }

    public IReadOnlySet<int> PlayerItemIds { get; init; } = new HashSet<int>();
}
