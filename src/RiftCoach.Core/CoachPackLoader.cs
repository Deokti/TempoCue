using System.Text.Json;
using System.Text.Json.Serialization;

namespace RiftCoach.Core;

public static class CoachPackLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static CoachPack LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static CoachPack Load(Stream jsonStream)
    {
        ArgumentNullException.ThrowIfNull(jsonStream);

        var pack = JsonSerializer.Deserialize<CoachPack>(jsonStream, SerializerOptions)
            ?? throw new InvalidDataException("Coach Pack is empty.");

        Validate(pack);
        return pack;
    }

    private static void Validate(CoachPack pack)
    {
        if (pack.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported Coach Pack schema: {pack.SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(pack.Id) || string.IsNullOrWhiteSpace(pack.Name))
        {
            throw new InvalidDataException("Coach Pack id and name are required.");
        }

        if (string.IsNullOrWhiteSpace(pack.Patch))
        {
            throw new InvalidDataException("Coach Pack patch is required.");
        }

        if (pack.Rules.Count == 0)
        {
            throw new InvalidDataException("Coach Pack must contain at least one rule.");
        }

        var duplicateRule = pack.Rules
            .GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateRule is not null)
        {
            throw new InvalidDataException($"Duplicate rule id: {duplicateRule.Key}.");
        }

        foreach (var rule in pack.Rules)
        {
            ValidateRule(rule);
        }
    }

    private static void ValidateRule(CoachRuleDefinition rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Id)
            || string.IsNullOrWhiteSpace(rule.PlayerChampion)
            || string.IsNullOrWhiteSpace(rule.OpponentChampion))
        {
            throw new InvalidDataException("Every rule requires an id and both champions.");
        }

        if (rule.MinimumLevel is < 1 or > 18
            || rule.MaximumLevel is < 1 or > 18
            || rule.MaximumLevel < rule.MinimumLevel)
        {
            throw new InvalidDataException($"Rule '{rule.Id}' has an invalid level range.");
        }

        if (rule.RequiredItemIds.Intersect(rule.ExcludedItemIds).Any())
        {
            throw new InvalidDataException($"Rule '{rule.Id}' requires and excludes the same item.");
        }

        if (rule.Priority < 0)
        {
            throw new InvalidDataException($"Rule '{rule.Id}' has a negative priority.");
        }

        if (string.IsNullOrWhiteSpace(rule.Title)
            || string.IsNullOrWhiteSpace(rule.Message)
            || string.IsNullOrWhiteSpace(rule.Source))
        {
            throw new InvalidDataException($"Rule '{rule.Id}' requires title, message and source.");
        }
    }
}
