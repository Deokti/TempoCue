namespace TempoCue.Core;

/// <summary>
/// Набор связанных тренерских правил, загружаемый из одного JSON-файла.
/// </summary>
public sealed record CoachPack
{
    /// <summary>Версия формата файла, необходимая для будущей совместимости.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>Уникальный идентификатор набора.</summary>
    public required string Id { get; init; }

    /// <summary>Название набора, понятное пользователю.</summary>
    public required string Name { get; init; }

    /// <summary>Патч League of Legends, для которого проверены советы.</summary>
    public required string Patch { get; init; }

    /// <summary>Краткое описание назначения набора.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Автор содержимого Coach Pack.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Язык пользовательских советов; приложение не переводит их автоматически.</summary>
    public string ContentLanguage { get; init; } = string.Empty;

    /// <summary>Чемпион пользователя для матчап-набора.</summary>
    public string PlayerChampion { get; init; } = string.Empty;

    /// <summary>Чемпион противника для матчап-набора.</summary>
    public string OpponentChampion { get; init; } = string.Empty;

    /// <summary>Роль, для которой автор проверял рекомендации.</summary>
    public string LaneRole { get; init; } = string.Empty;

    /// <summary>Дата последней проверки рекомендаций автором.</summary>
    public DateOnly? VerifiedOn { get; init; }

    /// <summary>Источник знаний и состояние разрешения на его использование.</summary>
    public CoachPackSource? Source { get; init; }

    /// <summary>Все правила, входящие в набор.</summary>
    public IReadOnlyList<CoachRuleDefinition> Rules { get; init; } = [];

    /// <summary>Подсказки, которые показываются весь матч или в выбранной фазе.</summary>
    public IReadOnlyList<CoachPersistentTipDefinition> PersistentTips { get; init; } = [];

    /// <summary>Произвольные авторские блоки предметов в заданном порядке.</summary>
    public IReadOnlyList<CoachItemBlockDefinition> ItemBlocks { get; init; } = [];
}
