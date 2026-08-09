namespace TempoCue.Core;

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
    /// Находит матчап активного игрока внутри общего состояния матча, после чего
    /// выбирает самое приоритетное подходящее правило.
    /// Возвращает null, если противника или подходящего правила определить не удалось.
    /// </summary>
    public CoachAdvice? GetBestAdvice(MatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var matchup = MatchupResolver.Resolve(match);
        return matchup is null ? null : GetBestAdvice(matchup);
    }

    /// <summary>
    /// Выбирает совет для уже определённого противостояния двух участников.
    /// Такой метод удобно использовать в тестах отдельных матчапов.
    /// </summary>
    public CoachAdvice? GetBestAdvice(MatchupContext matchup)
    {
        ArgumentNullException.ThrowIfNull(matchup);

        var rule = _rules
            // Оставляем только правила, все условия которых выполнены.
            .Where(candidate => Matches(candidate, matchup))
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
                rule.Source,
                rule.RequiredItemIds,
                rule.ExcludedItemIds);
    }

    private static bool Matches(CoachRuleDefinition rule, MatchupContext matchup)
    {
        // Сравниваем имена без учёта регистра: "Urgot" и "urgot" считаются равными.
        if (!string.Equals(rule.PlayerChampion, matchup.Player.ChampionName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rule.OpponentChampion, matchup.Opponent.ChampionName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (matchup.Player.Level < rule.MinimumLevel
            || rule.MaximumLevel is not null && matchup.Player.Level > rule.MaximumLevel)
        {
            return false;
        }

        // Any здесь отвечает на вопрос: отсутствует ли хотя бы один обязательный предмет?
        if (rule.RequiredItemIds.Any(itemId => !matchup.Player.ItemIds.Contains(itemId)))
        {
            return false;
        }

        // Все запрещённые предметы должны отсутствовать у игрока.
        return rule.ExcludedItemIds.All(itemId => !matchup.Player.ItemIds.Contains(itemId));
    }
}
