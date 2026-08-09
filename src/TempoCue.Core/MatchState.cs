namespace TempoCue.Core;

/// <summary>
/// Снимок всего активного матча в конкретный момент времени.
/// Позднее эту модель будет заполнять Riot API, а симулятор продолжит использовать
/// её для локальной проверки сценариев без запущенной League of Legends.
/// </summary>
public sealed record MatchState
{
    /// <summary>Время матча в секундах.</summary>
    public required double GameTimeSeconds { get; init; }

    /// <summary>Идентификатор участника, за которого играет пользователь.</summary>
    public required string ActivePlayerId { get; init; }

    /// <summary>Все известные участники обеих команд.</summary>
    public required IReadOnlyList<PlayerState> Players { get; init; }

    /// <summary>Находит активного игрока среди всех участников.</summary>
    public PlayerState? GetActivePlayer()
    {
        return Players.FirstOrDefault(player =>
            string.Equals(player.ParticipantId, ActivePlayerId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Возвращает союзников активного игрока, не включая его самого.</summary>
    public IReadOnlyList<PlayerState> GetAllies()
    {
        var activePlayer = GetActivePlayer();
        if (activePlayer is null || activePlayer.Team == TeamSide.Unknown)
        {
            return [];
        }

        return Players
            .Where(player => player.Team == activePlayer.Team
                && !string.Equals(
                    player.ParticipantId,
                    activePlayer.ParticipantId,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>Возвращает всех участников противоположной команды.</summary>
    public IReadOnlyList<PlayerState> GetEnemies()
    {
        var activePlayer = GetActivePlayer();
        if (activePlayer is null)
        {
            return [];
        }

        if (activePlayer.Team == TeamSide.Unknown)
        {
            return [];
        }

        return Players
            .Where(player => player.Team != TeamSide.Unknown
                && player.Team != activePlayer.Team)
            .ToArray();
    }
}
