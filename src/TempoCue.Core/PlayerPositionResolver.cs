namespace TempoCue.Core;

/// <summary>
/// Возвращает позицию игрока из Riot или безопасно восстанавливает одиночную роль
/// по полному составу команды, когда Riot оставил её неизвестной.
/// </summary>
public static class PlayerPositionResolver
{
    private static readonly PlayerPosition[] ExactPositions =
    [
        PlayerPosition.Top,
        PlayerPosition.Jungle,
        PlayerPosition.Middle,
        PlayerPosition.Bottom,
        PlayerPosition.Support
    ];

    public static PlayerPosition Resolve(MatchState match, PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(player);

        if (player.Position != PlayerPosition.Unknown)
        {
            return player.Position;
        }

        var teammates = match.Players
            .Where(candidate => candidate.Team == player.Team)
            .ToArray();

        if (player.Team == TeamSide.Unknown
            || teammates.Length != 5
            || teammates.Count(candidate => candidate.Position == PlayerPosition.Unknown) != 1)
        {
            return PlayerPosition.Unknown;
        }

        var exactPosition = ResolveOnlyMissingExactPosition(teammates);
        if (exactPosition != PlayerPosition.Unknown)
        {
            return exactPosition;
        }

        var topSlots = 1;
        var jungleSlots = 1;
        var middleSlots = 1;
        var bottomLaneSlots = 2;

        foreach (var teammate in teammates)
        {
            switch (teammate.Position)
            {
                case PlayerPosition.Top:
                    topSlots--;
                    break;
                case PlayerPosition.Jungle:
                    jungleSlots--;
                    break;
                case PlayerPosition.Middle:
                    middleSlots--;
                    break;
                case PlayerPosition.Bottom:
                case PlayerPosition.Support:
                    bottomLaneSlots--;
                    break;
            }
        }

        if (topSlots < 0 || jungleSlots < 0 || middleSlots < 0 || bottomLaneSlots < 0)
        {
            return PlayerPosition.Unknown;
        }

        var remainingSlots = topSlots + jungleSlots + middleSlots + bottomLaneSlots;
        if (remainingSlots != 1)
        {
            return PlayerPosition.Unknown;
        }

        if (topSlots == 1)
        {
            return PlayerPosition.Top;
        }

        if (jungleSlots == 1)
        {
            return PlayerPosition.Jungle;
        }

        return middleSlots == 1
            ? PlayerPosition.Middle
            : PlayerPosition.Unknown;
    }

    private static PlayerPosition ResolveOnlyMissingExactPosition(
        IReadOnlyList<PlayerState> teammates)
    {
        var missingPosition = PlayerPosition.Unknown;

        foreach (var position in ExactPositions)
        {
            var occupiedSlots = teammates.Count(player => player.Position == position);
            if (occupiedSlots > 1)
            {
                return PlayerPosition.Unknown;
            }

            if (occupiedSlots == 0)
            {
                if (missingPosition != PlayerPosition.Unknown)
                {
                    return PlayerPosition.Unknown;
                }

                missingPosition = position;
            }
        }

        return missingPosition;
    }
}
