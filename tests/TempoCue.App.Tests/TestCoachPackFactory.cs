using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Создаёт короткие детерминированные Coach Pack для UI-тестов.</summary>
internal static class TestCoachPackFactory
{
    public static CoachPack CreateMatchup(
        string id = "test.urgot-vs-sett",
        string name = "Urgot vs Sett",
        string patch = "test",
        IReadOnlyList<CoachRuleDefinition>? rules = null) => new()
        {
            SchemaVersion = 1,
            Id = id,
            Name = name,
            Patch = patch,
            Rules = rules ??
            [
                CreateRule(
                    "early",
                    1,
                    8,
                    PowerState.Dangerous,
                    "Test title")
            ]
        };

    public static CoachRuleDefinition CreateRule(
        string id,
        int minimumLevel,
        int? maximumLevel,
        PowerState powerState,
        string title) => new()
        {
            Id = id,
            PlayerChampion = "Urgot",
            OpponentChampion = "Sett",
            MinimumLevel = minimumLevel,
            MaximumLevel = maximumLevel,
            PowerState = powerState,
            Priority = 1,
            Title = title,
            Message = "Содержимое набора остаётся на языке автора.",
            Source = "Тестовый источник"
        };
}
