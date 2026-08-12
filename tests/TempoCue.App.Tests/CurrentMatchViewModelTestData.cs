using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

internal static class CurrentMatchViewModelTestData
{
    public static ActiveMatchMonitor CreateMonitor(
        IMatchSnapshotSource source,
        IMatchAdviceSelector? adviceSelector = null,
        TimeSpan? pollingInterval = null) =>
        new(new ActiveMatchSession(
            source,
            adviceSelector ?? new TestMatchAdviceSelector(),
            pollingInterval ?? TimeSpan.FromSeconds(1)));

    public static PlayerState CreatePlayer(
        string id,
        string champion,
        TeamSide team,
        PlayerPosition position,
        int creepScore = 0) => new()
        {
            ParticipantId = id,
            ChampionName = champion,
            Team = team,
            Position = position,
            Level = 1,
            CreepScore = creepScore
        };
}
