namespace RiftCoach.Core;

/// <summary>
/// Готовый совет, который движок возвращает интерфейсу приложения.
/// </summary>
/// <param name="RuleId">Идентификатор сработавшего правила.</param>
/// <param name="Title">Короткий заголовок для пользователя.</param>
/// <param name="Message">Основной текст совета.</param>
/// <param name="PowerState">Оценка текущей силы в матчапе.</param>
/// <param name="Priority">Приоритет правила среди других подходящих правил.</param>
/// <param name="Source">Источник и пояснение происхождения совета.</param>
public sealed record CoachAdvice(
    string RuleId,
    string Title,
    string Message,
    PowerState PowerState,
    int Priority,
    string Source);
