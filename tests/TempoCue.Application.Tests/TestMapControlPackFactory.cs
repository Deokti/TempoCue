using TempoCue.Core;

namespace TempoCue.Application.Tests;

internal static class TestMapControlPackFactory
{
    internal static MapControlPack Create(string id = "map-control.test") =>
        new()
        {
            SchemaVersion = MapControlPackValidator.CurrentSchemaVersion,
            PackType = MapControlPack.SupportedPackType,
            Id = id,
            Name = "Контроль объектов",
            Patch = "16.15",
            Description = "Тестовый набор контроля карты.",
            Author = "TestAuthor",
            ContentLanguage = "ru-RU",
            VerifiedOn = new DateOnly(2026, 8, 12),
            Source = new CoachPackSource
            {
                Kind = CoachPackSourceKind.PersonalExperience,
                Permission = CoachPackSourcePermission.NotRequired
            },
            Rules =
            [
                new MapObjectiveReminderRule(
                    "dragon-before-spawn",
                    MapObjectiveKind.ElementalDragon,
                    MapObjectiveReminderTrigger.BeforeInitialSpawn,
                    TimeSpan.FromSeconds(45),
                    MapObjectiveReminderRecurrence.OncePerMatch,
                    MapObjectiveReminderPriority.Important,
                    "Подготовьтесь к дракону",
                    "Освободите линию заранее.",
                    "До появления осталось меньше минуты.",
                    "Подойти к реке вместе с командой.")
            ]
        };
}
