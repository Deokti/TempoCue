namespace TempoCue.Core;

/// <summary>
/// Узкий контекст конкретного противостояния на линии, выделенный из всего матча.
/// </summary>
/// <param name="Player">Активный игрок.</param>
/// <param name="Opponent">Определённый противник на той же позиции.</param>
public sealed record MatchupContext(PlayerState Player, PlayerState Opponent);
