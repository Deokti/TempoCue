using TempoCue.Core;

namespace TempoCue.App.Tests;

internal static class CurrentMatchViewModelTestData
{
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
