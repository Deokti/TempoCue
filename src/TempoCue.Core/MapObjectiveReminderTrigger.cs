namespace TempoCue.Core;

/// <summary>Игровой факт, относительно которого планируется напоминание об объекте.</summary>
public enum MapObjectiveReminderTrigger
{
    BeforeInitialSpawn,
    AtInitialSpawn,
    BeforeRespawn,
    AfterKill
}
