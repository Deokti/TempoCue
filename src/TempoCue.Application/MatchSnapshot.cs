using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Один успешно прочитанный снимок активного матча.</summary>
public sealed record MatchSnapshot(
    MatchState Match,
    IReadOnlyList<MatchEvent> Events,
    DateTimeOffset ReceivedAtUtc);
