using System.Text.Json.Serialization;

namespace TempoCue.Riot;

internal sealed record LeagueClientChampionSelectSessionDto
{
    [JsonPropertyName("localPlayerCellId")]
    public int LocalPlayerCellId { get; init; } = -1;

    [JsonPropertyName("myTeam")]
    public IReadOnlyList<LeagueClientChampionSelectParticipantDto>? MyTeam { get; init; }
}

internal sealed record LeagueClientChampionSelectParticipantDto
{
    [JsonPropertyName("cellId")]
    public int CellId { get; init; } = -1;

    [JsonPropertyName("championId")]
    public int ChampionId { get; init; }

    [JsonPropertyName("summonerId")]
    public long SummonerId { get; init; }

    [JsonPropertyName("assignedPosition")]
    public string? AssignedPosition { get; init; }

    [JsonPropertyName("nameVisibilityType")]
    public string? NameVisibilityType { get; init; }
}

internal sealed record LeagueClientSummonerDto
{
    [JsonPropertyName("gameName")]
    public string? GameName { get; init; }

    [JsonPropertyName("tagLine")]
    public string? TagLine { get; init; }
}
