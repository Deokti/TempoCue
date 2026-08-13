namespace TempoCue.Coaching;

/// <summary>Проверяет формат и непротиворечивость Coach Pack на границе системы.</summary>
public static class CoachPackValidator
{
    public static void Validate(CoachPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        if (pack.SchemaVersion is not (1 or 2))
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

        CoachPackValidation.EnsureUniqueIds(pack.Rules.Select(static rule => rule.Id), "rule");
        foreach (var rule in pack.Rules)
        {
            ValidateRule(rule);
        }

        if (pack.SchemaVersion == 2)
        {
            ValidateVersionTwo(pack);
        }
    }

    private static void ValidateVersionTwo(CoachPack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.Description)
            || string.IsNullOrWhiteSpace(pack.Author)
            || string.IsNullOrWhiteSpace(pack.ContentLanguage)
            || string.IsNullOrWhiteSpace(pack.PlayerChampion)
            || string.IsNullOrWhiteSpace(pack.OpponentChampion)
            || string.IsNullOrWhiteSpace(pack.LaneRole)
            || pack.VerifiedOn is null
            || pack.Source is null)
        {
            throw new InvalidDataException("Coach Pack schema 2 metadata is incomplete.");
        }

        if (string.Equals(
                pack.PlayerChampion,
                pack.OpponentChampion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Matchup champions must be different.");
        }

        if (string.Equals(pack.Id, pack.ForkedFromPackId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A Coach Pack copy must have its own id.");
        }

        if (pack.ForkedFromAuthor is not null && string.IsNullOrWhiteSpace(pack.ForkedFromPackId))
        {
            throw new InvalidDataException("Fork author requires the source Coach Pack id.");
        }

        CoachPackValidation.ValidateSource(pack.Source);
        CoachPackValidation.EnsureUniqueIds(
            pack.PersistentTips.Select(static tip => tip.Id),
            "persistent tip");
        CoachPackValidation.EnsureUniqueIds(pack.ItemBlocks.Select(static block => block.Id), "item block");

        var ruleIds = pack.Rules.Select(static rule => rule.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tip in pack.PersistentTips)
        {
            if (string.IsNullOrWhiteSpace(tip.Title) || string.IsNullOrWhiteSpace(tip.Message))
            {
                throw new InvalidDataException($"Persistent tip '{tip.Id}' requires title and message.");
            }

            if (tip.ApplicableRuleId is not null && !ruleIds.Contains(tip.ApplicableRuleId))
            {
                throw new InvalidDataException(
                    $"Persistent tip '{tip.Id}' references an unknown rule.");
            }
        }

        foreach (var block in pack.ItemBlocks)
        {
            if (string.IsNullOrWhiteSpace(block.Name))
            {
                throw new InvalidDataException($"Item block '{block.Id}' requires a name.");
            }

            EnsureValidItemIds(block.ItemIds, $"Item block '{block.Id}'");
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

        EnsureValidItemIds(rule.RequiredItemIds, $"Rule '{rule.Id}' required items");
        EnsureValidItemIds(rule.ExcludedItemIds, $"Rule '{rule.Id}' excluded items");
        EnsureValidItemIds(rule.RecommendedItemIds, $"Rule '{rule.Id}' recommended items");

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

    private static void EnsureValidItemIds(IEnumerable<int> itemIds, string owner)
    {
        var items = itemIds.ToArray();
        if (items.Any(static itemId => itemId <= 0) || items.Distinct().Count() != items.Length)
        {
            throw new InvalidDataException($"{owner} contains invalid or duplicate item ids.");
        }
    }
}
