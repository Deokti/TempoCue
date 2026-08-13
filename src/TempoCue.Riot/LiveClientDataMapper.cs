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

    /// <summary>
    /// Преобразует только подтверждённые факты, которые уже поддерживает продукт.
    /// Неизвестные события остаются в исходном JSON и не превращаются в догадки.
    /// </summary>
    public static IReadOnlyList<MatchEvent> MapEvents(LiveClientAllGameDataDto source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Events.Events
            .Select(eventDto => MapEvent(eventDto, source.AllPlayers))
            .Where(matchEvent => matchEvent is not null)
            .Cast<MatchEvent>()
            .OrderByDescending(matchEvent => matchEvent.GameTimeSeconds)
            .ToArray();
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
            IsBot = source.IsBot,
            Kills = source.Scores.Kills,
            Deaths = source.Scores.Deaths,
            Assists = source.Scores.Assists,
            CreepScore = source.Scores.CreepScore
        };
    }

    private static MatchEvent? MapEvent(
        LiveClientEventDto source,
        IReadOnlyList<LiveClientPlayerDto> players)
    {
        var kind = source.EventName switch
        {
            "ChampionKill" => MatchEventKind.ChampionKill,
            "DragonKill" => MatchEventKind.DragonKill,
            "HeraldKill" => MatchEventKind.RiftHeraldKill,
            "BaronKill" => MatchEventKind.BaronNashorKill,
            _ => (MatchEventKind?)null
        };

        if (kind is null)
        {
            return null;
        }

        return new MatchEvent
        {
            EventId = source.EventId,
            Kind = kind.Value,
            GameTimeSeconds = source.EventTime,
            KillerParticipantId = ResolveParticipantId(source.KillerName, players),
            VictimParticipantId = ResolveParticipantId(source.VictimName, players),
            AssisterParticipantIds = source.Assisters
                .Select(name => ResolveParticipantId(name, players))
                .Where(participantId => participantId is not null)
                .Cast<string>()
                .ToArray(),
            DragonType = MapDragonType(source.DragonType)
        };
    }

    private static DragonType MapDragonType(string? dragonType)
    {
        return dragonType?.ToUpperInvariant() switch
        {
            "AIR" => DragonType.Air,
            "EARTH" => DragonType.Earth,
            "FIRE" => DragonType.Fire,
            "WATER" => DragonType.Water,
            "HEXTECH" => DragonType.Hextech,
            "CHEMTECH" => DragonType.Chemtech,
            "ELDER" => DragonType.Elder,
            _ => DragonType.Unknown
        };
    }

    private static string? ResolveParticipantId(
        string? externalName,
        IReadOnlyList<LiveClientPlayerDto> players)
    {
        if (string.IsNullOrWhiteSpace(externalName))
        {
            return null;
        }

        var player = players.FirstOrDefault(candidate =>
            EqualsName(candidate.RiotId, externalName)
            || EqualsName(candidate.RiotIdGameName, externalName)
            || EqualsName(candidate.SummonerName, externalName));

        return player is null
            ? null
            : GetParticipantId(player.RiotId, player.SummonerName);
    }

    private static bool EqualsName(string? candidate, string value) =>
        string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase);

    internal static string? GetParticipantId(string? riotId, string? summonerName)
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
