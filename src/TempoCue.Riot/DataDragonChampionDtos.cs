using System.Text.Json.Serialization;

namespace TempoCue.Riot;

internal sealed class DataDragonChampionListDto
{
    [JsonPropertyName("data")]
    public Dictionary<string, DataDragonChampionDto>? Data { get; init; }
}

internal sealed class DataDragonChampionDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("image")]
    public DataDragonImageDto? Image { get; init; }
}

internal sealed class DataDragonImageDto
{
    [JsonPropertyName("full")]
    public string? Full { get; init; }
}

internal sealed class DataDragonChampionCacheDto
{
    public string? Version { get; init; }

    public string? Locale { get; init; }

    public DateTimeOffset SavedAtUtc { get; init; }

    public List<DataDragonCachedChampionDto>? Champions { get; init; }
}

internal sealed class DataDragonCachedChampionDto
{
    public string? Id { get; init; }

    public string? DisplayName { get; init; }

    public string? ImageUrl { get; init; }
}
