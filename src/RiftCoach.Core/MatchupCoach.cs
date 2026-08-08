namespace RiftCoach.Core;

public sealed class MatchupCoach
{
    private readonly IReadOnlyList<CoachRuleDefinition> _rules;

    public MatchupCoach(IEnumerable<CoachRuleDefinition> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
    }

    public CoachAdvice? GetBestAdvice(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rule = _rules
            .Where(candidate => Matches(candidate, state))
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        return rule is null
            ? null
            : new CoachAdvice(
                rule.Id,
                rule.Title,
                rule.Message,
                rule.PowerState,
                rule.Priority,
                rule.Source);
    }

    private static bool Matches(CoachRuleDefinition rule, GameState state)
    {
        if (!string.Equals(rule.PlayerChampion, state.PlayerChampion, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rule.OpponentChampion, state.OpponentChampion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (state.PlayerLevel < rule.MinimumLevel
            || rule.MaximumLevel is not null && state.PlayerLevel > rule.MaximumLevel)
        {
            return false;
        }

        if (rule.RequiredItemIds.Any(itemId => !state.PlayerItemIds.Contains(itemId)))
        {
            return false;
        }

        return rule.ExcludedItemIds.All(itemId => !state.PlayerItemIds.Contains(itemId));
    }
}
