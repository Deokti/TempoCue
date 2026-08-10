using System.Text.Json.Serialization;

namespace TempoCue.Riot;

internal sealed class DataDragonItemListDto
{
    [JsonPropertyName("data")]
    public Dictionary<string, DataDragonItemDto>? Data { get; init; }
}

internal sealed class DataDragonItemDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("plaintext")]
    public string? PlainText { get; init; }

    [JsonPropertyName("image")]
    public DataDragonImageDto? Image { get; init; }

    [JsonPropertyName("gold")]
    public DataDragonItemGoldDto? Gold { get; init; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }

    [JsonPropertyName("maps")]
    public Dictionary<string, bool>? Maps { get; init; }

    [JsonPropertyName("inStore")]
    public bool? InStore { get; init; }
}

internal sealed class DataDragonItemGoldDto
{
    [JsonPropertyName("purchasable")]
    public bool Purchasable { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }
}

internal sealed class DataDragonItemCacheDto
{
    public string? Version { get; init; }

    public string? GamePatch { get; init; }

    public string? Locale { get; init; }

    public DateTimeOffset SavedAtUtc { get; init; }

    public List<DataDragonCachedItemDto>? Items { get; init; }
}

internal sealed class DataDragonCachedItemDto
{
    public int Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public int Price { get; init; }

    public List<string>? Tags { get; init; }
}
