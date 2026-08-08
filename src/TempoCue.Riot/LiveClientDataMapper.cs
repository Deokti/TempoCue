using TempoCue.Core;

namespace TempoCue.Riot;

/// <summary>
/// Преобразует транспортные данные Riot в независимую модель TempoCue.
/// Благодаря этому остальное приложение не зависит от формы внешнего JSON.
/// </summary>
internal static class LiveClientDataMapper
{
    public static MatchState Map(LiveClientAllGameDataDto source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var activePlayerId = GetParticipantId(
            source.ActivePlayer.RiotId,
            source.ActivePlayer.SummonerName);

        if (activePlayerId is null)
        {
            throw new InvalidDataException("Riot API не вернул идентификатор активного игрока.");
        }

        var players = source.AllPlayers
            .Select(MapPlayer)
            .ToArray();

        return new MatchState
        {
            GameTimeSeconds = source.GameData.GameTime,
            ActivePlayerId = activePlayerId,
            Players = players
        };
    }

    private static PlayerState MapPlayer(LiveClientPlayerDto source, int index)
    {
        var participantId = GetParticipantId(source.RiotId, source.SummonerName)
            ?? $"participant-{index}";

        return new PlayerState
        {
            ParticipantId = participantId,
            ChampionName = GetCanonicalChampionName(source),
            Team = MapTeam(source.Team),
            Position = MapPosition(source.Position),
            Level = source.Level,
            ItemIds = source.Items.Select(item => item.ItemId).ToHashSet(),
            IsDead = source.IsDead,
            RespawnTimerSeconds = source.RespawnTimer,
            IsBot = source.IsBot
        };
    }

    private static string? GetParticipantId(string? riotId, string? summonerName)
    {
        if (!string.IsNullOrWhiteSpace(riotId))
        {
            return riotId;
        }

        return string.IsNullOrWhiteSpace(summonerName) ? null : summonerName;
    }

    private static string GetCanonicalChampionName(LiveClientPlayerDto source)
    {
        const string displayNamePrefix = "game_character_displayname_";

        // championName зависит от языка клиента («Ургот»), поэтому правила используют
        // стабильную часть rawChampionName (Urgot). Если Riot не прислал её,
        // сохраняем отображаемое имя как запасной вариант.
        if (!string.IsNullOrWhiteSpace(source.RawChampionName)
            && source.RawChampionName.StartsWith(
                displayNamePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return source.RawChampionName[displayNamePrefix.Length..];
        }

        return source.ChampionName;
    }

    private static TeamSide MapTeam(string? team)
    {
        return team?.ToUpperInvariant() switch
        {
            "ORDER" => TeamSide.Order,
            "CHAOS" => TeamSide.Chaos,
            _ => TeamSide.Unknown
        };
    }

    private static PlayerPosition MapPosition(string? position)
    {
        return position?.ToUpperInvariant() switch
        {
            "TOP" => PlayerPosition.Top,
            "JUNGLE" => PlayerPosition.Jungle,
            "MIDDLE" => PlayerPosition.Middle,
            "BOTTOM" => PlayerPosition.Bottom,
            "UTILITY" => PlayerPosition.Support,
            _ => PlayerPosition.Unknown
        };
    }
}
