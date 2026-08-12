namespace TempoCue.Coaching;

/// <summary>Переносимый набор напоминаний о нейтральных объектах карты.</summary>
public sealed record MapControlPack
{
    /// <summary>Значение, отличающее набор контроля карты от матчап-набора при импорте.</summary>
    public const string SupportedPackType = "mapControl";

    /// <summary>Версия схемы переносимого файла.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>Тип переносимого набора.</summary>
    public required string PackType { get; init; }

    /// <summary>Уникальный идентификатор набора.</summary>
    public required string Id { get; init; }

    /// <summary>Название набора, отображаемое пользователю.</summary>
    public required string Name { get; init; }

    /// <summary>Патч League of Legends, на котором проверено содержимое.</summary>
    public required string Patch { get; init; }

    /// <summary>Краткое описание назначения набора.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Автор содержимого.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Язык пользовательских напоминаний.</summary>
    public string ContentLanguage { get; init; } = string.Empty;

    /// <summary>Дата последней проверки правил автором.</summary>
    public DateOnly? VerifiedOn { get; init; }

    /// <summary>Источник знаний и состояние разрешения на его использование.</summary>
    public CoachPackSource? Source { get; init; }

    /// <summary>Идентификатор исходного набора, если пользователь создал свою копию.</summary>
    public string? ForkedFromPackId { get; init; }

    /// <summary>Автор исходного набора на момент создания копии.</summary>
    public string? ForkedFromAuthor { get; init; }

    /// <summary>Роли и сторона команды, для которых набор может быть активирован.</summary>
    public MapControlApplicability Applicability { get; init; } = new();

    /// <summary>Правила напоминаний в стабильном авторском порядке.</summary>
    public IReadOnlyList<MapObjectiveReminderRule> Rules { get; init; } = [];

    /// <summary>Одноразовые напоминания, привязанные к игровому времени.</summary>
    public IReadOnlyList<MapControlTimedReminder> TimedReminders { get; init; } = [];

    /// <summary>Справочные подсказки в стабильном авторском порядке.</summary>
    public IReadOnlyList<MapControlPersistentTip> PersistentTips { get; init; } = [];
}
