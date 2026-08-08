using System.Text.Json.Serialization;

namespace RiftCoach.Riot;

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
}

internal sealed record LiveClientItemDto
{
    // В JSON Riot используется написание itemID, поэтому имя задаётся явно.
    [JsonPropertyName("itemID")]
    public int ItemId { get; init; }
}
