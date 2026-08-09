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

    /// <summary>Все правила, входящие в набор.</summary>
    public IReadOnlyList<CoachRuleDefinition> Rules { get; init; } = [];
}
