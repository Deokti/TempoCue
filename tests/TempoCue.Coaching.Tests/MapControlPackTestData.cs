using TempoCue.Core;

namespace TempoCue.Coaching.Tests;

internal static class MapControlPackTestData
{
    internal static MapControlPack Create() =>
        new()
        {
            SchemaVersion = MapControlPackValidator.CurrentSchemaVersion,
            PackType = MapControlPack.SupportedPackType,
            Id = "map-control.standard.test",
            Name = "Стандартный контроль объектов",
            Patch = "16.15",
            Description = "Напоминает о подготовке к нейтральным объектам.",
            Author = "TempoCueAuthor",
            ContentLanguage = "ru-RU",
            VerifiedOn = new DateOnly(2026, 8, 12),
            Source = new CoachPackSource
            {
                Kind = CoachPackSourceKind.PersonalExperience,
                Permission = CoachPackSourcePermission.NotRequired
            },
            Applicability = new MapControlApplicability
            {
                Roles = [MapControlRole.Top, MapControlRole.Support],
                TeamSide = MapControlTeamSide.Blue
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
                    "Освободите линию и поставьте обзор заранее.",
                    "До первого дракона осталось меньше минуты.",
                    "Подойти к реке вместе с командой."),
                new MapObjectiveReminderRule(
                    "dragon-before-respawn",
                    MapObjectiveKind.ElementalDragon,
                    MapObjectiveReminderTrigger.BeforeRespawn,
                    TimeSpan.FromMinutes(1),
                    MapObjectiveReminderRecurrence.EveryCycle,
                    MapObjectiveReminderPriority.Caution,
                    "Дракон скоро возродится",
                    "Подготовьте обзор и завершите покупку.",
                    "Убийство дракона подтверждено игровым событием.",
                    "Собраться у нижней реки.")
            ]
        };
}
