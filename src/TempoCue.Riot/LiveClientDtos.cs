using System.Text.Json.Serialization;

namespace TempoCue.Riot;

/// <summary>
/// Внутренние DTO повторяют только ту часть большого allgamedata,
/// которая необходима для текущей бизнес-модели.
/// Неизвестные поля JSON System.Text.Json автоматически пропускает.
/// </summary>
internal sealed record LiveClientAllGameDataDto
{
    public required LiveClientActivePlayerDto ActivePlayer { get; init; }

    public IReadOnlyList<LiveClientPlayerDto> AllPlayers { get; init; } = [];

    public required LiveClientGameDataDto GameData { get; init; }

    public LiveClientEventDataDto Events { get; init; } = new();
}

internal sealed record LiveClientActivePlayerDto
{
    public string? RiotId { get; init; }

    public string? SummonerName { get; init; }
}

internal sealed record LiveClientGameDataDto
{
    public double GameTime { get; init; }
}

internal sealed record LiveClientPlayerDto
{
    public string? RiotId { get; init; }

    public string? RiotIdGameName { get; init; }

    public string? RiotIdTagLine { get; init; }

    public string? SummonerName { get; init; }

    public required string ChampionName { get; init; }

    /// <summary>
    /// Нелокализованный внутренний ключ чемпиона, например
    /// game_character_displayname_Urgot при русском championName «Ургот».
    /// </summary>
    public string? RawChampionName { get; init; }

    public string? Team { get; init; }

    public string? Position { get; init; }

    public int Level { get; init; }

    public IReadOnlyList<LiveClientItemDto> Items { get; init; } = [];

    public bool IsDead { get; init; }

    public double RespawnTimer { get; init; }

    public bool IsBot { get; init; }

    public LiveClientScoresDto Scores { get; init; } = new();
}

internal sealed record LiveClientScoresDto
{
    public int Kills { get; init; }

    public int Deaths { get; init; }

    public int Assists { get; init; }

    public int CreepScore { get; init; }
}

internal sealed record LiveClientItemDto
{
    // В JSON Riot используется написание itemID, поэтому имя задаётся явно.
    [JsonPropertyName("itemID")]
    public int ItemId { get; init; }
}

internal sealed record LiveClientEventDataDto
{
    public IReadOnlyList<LiveClientEventDto> Events { get; init; } = [];
}

internal sealed record LiveClientEventDto
{
    public int EventId { get; init; }

    public required string EventName { get; init; }

    public double EventTime { get; init; }

    public string? KillerName { get; init; }

    public string? VictimName { get; init; }

    public IReadOnlyList<string> Assisters { get; init; } = [];

    public string? DragonType { get; init; }
}
