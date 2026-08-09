namespace TempoCue.Core;

/// <summary>
/// Определяет противника активного игрока на линии среди всех участников матча.
/// </summary>
public static class MatchupResolver
{
    /// <summary>
    /// Сначала ищет единственного врага с той же известной позицией.
    /// Для тестового матча 1×1 используется запасной вариант: единственный враг
    /// считается противником даже при неизвестной позиции.
    /// </summary>
    public static MatchupContext? Resolve(MatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var activePlayer = match.GetActivePlayer();
        if (activePlayer is null)
        {
            return null;
        }

        var enemies = match.GetEnemies();
        var activePosition = PlayerPositionResolver.Resolve(match, activePlayer);

        if (activePosition != PlayerPosition.Unknown)
        {
            var enemiesOnSamePosition = enemies
                .Select(enemy => enemy with
                {
                    Position = PlayerPositionResolver.Resolve(match, enemy)
                })
                .Where(enemy => enemy.Position == activePosition)
                .ToArray();

            if (enemiesOnSamePosition.Length == 1)
            {
                return new MatchupContext(
                    activePlayer with { Position = activePosition },
                    enemiesOnSamePosition[0]);
            }
        }

        return enemies.Count == 1
            ? new MatchupContext(activePlayer, enemies[0])
            : null;
    }

}
