namespace TempoCue.Coaching;

/// <summary>
/// Готовый совет, который движок возвращает интерфейсу приложения.
/// </summary>
/// <param name="RuleId">Идентификатор сработавшего правила.</param>
/// <param name="Title">Короткий заголовок для пользователя.</param>
/// <param name="Message">Основной текст совета.</param>
/// <param name="PowerState">Оценка текущей силы в матчапе.</param>
/// <param name="Priority">Приоритет правила среди других подходящих правил.</param>
/// <param name="Source">Источник и пояснение происхождения совета.</param>
/// <param name="RequiredItemIds">Предметы, наличие которых потребовало сработавшее правило.</param>
/// <param name="ExcludedItemIds">Предметы, отсутствие которых потребовало сработавшее правило.</param>
/// <param name="WhyNow">Объяснение актуальности совета в текущей фазе.</param>
/// <param name="NextGoal">Следующая цель, приближающая смену фазы.</param>
/// <param name="RecommendedItemIds">Предметы, рекомендуемые автором для этой фазы.</param>
public sealed record CoachAdvice(
    string RuleId,
    string Title,
    string Message,
    PowerState PowerState,
    int Priority,
    string Source,
    IReadOnlyList<int> RequiredItemIds,
    IReadOnlyList<int> ExcludedItemIds,
    string WhyNow,
    string NextGoal,
    IReadOnlyList<int> RecommendedItemIds);
