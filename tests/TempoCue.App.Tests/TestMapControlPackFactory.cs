using TempoCue.Core;

namespace TempoCue.App.Tests;

internal static class TestMapControlPackFactory
{
    internal static MapControlPack Create(string id = "map-control.store-test") =>
        new()
        {
            SchemaVersion = MapControlPackValidator.CurrentSchemaVersion,
            PackType = MapControlPack.SupportedPackType,
            Id = id,
            Name = "Контроль объектов",
            Patch = "16.15",
            Description = "Тест локального хранилища контроля карты.",
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
                    "Освободите линию заранее.",
                    "До появления осталось меньше минуты.",
                    "Подойти к реке вместе с командой.")
            ]
        };
}
