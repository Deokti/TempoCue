namespace TempoCue.Core;

/// <summary>
/// Одно правило из Coach Pack: условия его срабатывания и выдаваемый совет.
/// Свойства этого типа заполняются из JSON-файла.
/// </summary>
public sealed record CoachRuleDefinition
{
    /// <summary>Уникальный идентификатор правила внутри набора.</summary>
    public required string Id { get; init; }

    /// <summary>Чемпион пользователя, для которого предназначено правило.</summary>
    public required string PlayerChampion { get; init; }

    /// <summary>Противник, для которого предназначено правило.</summary>
    public required string OpponentChampion { get; init; }

    /// <summary>Минимальный уровень, начиная с которого правило действует.</summary>
    public int MinimumLevel { get; init; } = 1;

    /// <summary>
    /// Максимальный уровень действия правила. Значение null означает отсутствие верхней границы.
    /// </summary>
    public int? MaximumLevel { get; init; }

    /// <summary>Предметы, которые обязательно должны быть куплены.</summary>
    public IReadOnlyList<int> RequiredItemIds { get; init; } = [];

    /// <summary>Предметы, которых у игрока быть не должно.</summary>
    public IReadOnlyList<int> ExcludedItemIds { get; init; } = [];

    /// <summary>Оценка силы, которую покажет сработавшее правило.</summary>
    public PowerState PowerState { get; init; }

    /// <summary>Чем больше значение, тем важнее правило.</summary>
    public int Priority { get; init; }

    /// <summary>Короткий заголовок совета.</summary>
    public required string Title { get; init; }

    /// <summary>Основной текст совета.</summary>
    public required string Message { get; init; }

    /// <summary>Короткое объяснение, почему совет актуален в этой фазе.</summary>
    public string WhyNow { get; init; } = string.Empty;

    /// <summary>Следующая цель, приближающая переход к другой фазе.</summary>
    public string NextGoal { get; init; } = string.Empty;

    /// <summary>
    /// Рекомендуемые предметы фазы. Они не являются условием выбора правила.
    /// </summary>
    public IReadOnlyList<int> RecommendedItemIds { get; init; } = [];

    /// <summary>Источник знания или пометка о необходимости проверки.</summary>
    public required string Source { get; init; }
}
