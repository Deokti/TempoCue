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

    public static CoachPack CreateDetailedMatchup() => new()
    {
        SchemaVersion = 2,
        Id = "test.urgot-vs-sett.detailed",
        Name = "Urgot против Sett",
        Patch = "26.15",
        Description = "Проверяемый тестовый матчап с фазами, подсказками и предметами.",
        Author = "TestAuthor",
        ContentLanguage = "ru-RU",
        PlayerChampion = "Urgot",
        OpponentChampion = "Sett",
        LaneRole = "top",
        VerifiedOn = new DateOnly(2026, 8, 10),
        Source = new CoachPackSource
        {
            Kind = CoachPackSourceKind.PersonalExperience,
            Permission = CoachPackSourcePermission.NotRequired
        },
        Rules =
        [
            CreateRule(
                "early",
                1,
                8,
                PowerState.Dangerous,
                "Осторожное начало"),
            CreateRule(
                "late",
                9,
                null,
                PowerState.Playable,
                "Полный скачок силы") with
            {
                RecommendedItemIds = [3071]
            }
        ],
        PersistentTips =
        [
            new CoachPersistentTipDefinition
            {
                Id = "tip-1",
                Title = "Следи за Haymaker",
                Message = "Не стой в центральной зоне W.",
                ApplicableRuleId = "early"
            }
        ],
        ItemBlocks =
        [
            new CoachItemBlockDefinition
            {
                Id = "core",
                Name = "Ключевая сборка",
                WhenToChoose = "Основная линия сборки",
                ItemIds = [3071]
            },
            new CoachItemBlockDefinition
            {
                Id = "boots",
                Name = "Ботинки против AD",
                ItemIds = [3047]
            }
        ]
    };
}
