namespace RiftCoach.Core;

/// <summary>
/// Детерминированный движок матчап-советов.
/// Он не использует нейросеть: одинаковое состояние всегда даёт одинаковый результат.
/// </summary>
public sealed class MatchupCoach
{
    // Поле readonly можно заполнить в конструкторе, но нельзя заменить позднее.
    private readonly IReadOnlyList<CoachRuleDefinition> _rules;

    /// <summary>Создаёт движок и сохраняет собственный снимок переданных правил.</summary>
    public MatchupCoach(IEnumerable<CoachRuleDefinition> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
    }

    /// <summary>
    /// Находит самое приоритетное правило, соответствующее состоянию матча.
    /// Возвращает null, если подходящего правила нет.
    /// </summary>
    public CoachAdvice? GetBestAdvice(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rule = _rules
            // Оставляем только правила, все условия которых выполнены.
            .Where(candidate => Matches(candidate, state))
            // Если совпало несколько правил, сначала выбираем наибольший приоритет.
            .OrderByDescending(candidate => candidate.Priority)
            // ID обеспечивает стабильный результат при одинаковом приоритете.
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        // Тернарный оператор: при отсутствии правила возвращаем null,
        // иначе создаём готовый совет для интерфейса.
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
        // Сравниваем имена без учёта регистра: "Urgot" и "urgot" считаются равными.
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

        // Any здесь отвечает на вопрос: отсутствует ли хотя бы один обязательный предмет?
        if (rule.RequiredItemIds.Any(itemId => !state.PlayerItemIds.Contains(itemId)))
        {
            return false;
        }

        // Все запрещённые предметы должны отсутствовать у игрока.
        return rule.ExcludedItemIds.All(itemId => !state.PlayerItemIds.Contains(itemId));
    }
}
