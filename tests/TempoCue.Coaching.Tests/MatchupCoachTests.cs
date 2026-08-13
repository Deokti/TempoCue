using System.Text;

namespace TempoCue.Coaching.Tests;

public sealed class MatchupCoachTests
{
    private const int BlackCleaverItemId = 3071;

    [Fact]
    public void GetBestAdvice_ReturnsDangerousAdvice_ForEarlyLane()
    {
        var advice = CreateCoach().GetBestAdvice(CreateMatch(level: 1));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Dangerous, advice.PowerState);
        Assert.Equal("early", advice.RuleId);
    }

    [Fact]
    public void GetBestAdvice_ReturnsCautiousAdvice_AtLevelNineWithoutItem()
    {
        var advice = CreateCoach().GetBestAdvice(CreateMatch(level: 9));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Cautious, advice.PowerState);
        Assert.Equal("waiting-item", advice.RuleId);
        Assert.Contains(BlackCleaverItemId, advice.ExcludedItemIds);
    }

    [Fact]
    public void GetBestAdvice_ReturnsPlayableAdvice_WhenPowerSpikeIsComplete()
    {
        var advice = CreateCoach().GetBestAdvice(
            CreateMatch(level: 9, itemIds: [BlackCleaverItemId]));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Playable, advice.PowerState);
        Assert.Equal("power-spike", advice.RuleId);
        Assert.Contains(BlackCleaverItemId, advice.RequiredItemIds);
    }

    [Fact]
    public void GetBestAdvice_ReturnsNull_ForUnknownMatchup()
    {
        var advice = CreateCoach().GetBestAdvice(
            CreateMatch(level: 1, opponentChampion: "Garen"));

        Assert.Null(advice);
    }

    [Fact]
    public void GetBestAdvice_UsesAllParticipantsAndSelectsTopMatchup()
    {
        var advice = CreateCoach().GetBestAdvice(
            CreateFullMatch(level: 9, itemIds: [BlackCleaverItemId]));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Playable, advice.PowerState);
        Assert.Equal("power-spike", advice.RuleId);
    }

    [Fact]
    public void CoachPackLoader_RejectsRuleThatRequiresAndExcludesSameItem()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "invalid",
              "name": "Invalid",
              "patch": "test",
              "rules": [
                {
                  "id": "invalid-rule",
                  "playerChampion": "Urgot",
                  "opponentChampion": "Sett",
                  "minimumLevel": 1,
                  "requiredItemIds": [3071],
                  "excludedItemIds": [3071],
                  "powerState": "dangerous",
                  "priority": 1,
                  "title": "Invalid",
                  "message": "Invalid",
                  "source": "Test"
                }
              ]
            }
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => CoachPackLoader.Load(stream));
    }

    private static MatchupCoach CreateCoach() => new(
        [
            CreateRule("early", 1, PowerState.Dangerous, 80, maximumLevel: 8),
            CreateRule(
                "waiting-item",
                9,
                PowerState.Cautious,
                85,
                excludedItemIds: [BlackCleaverItemId]),
            CreateRule(
                "power-spike",
                9,
                PowerState.Playable,
                100,
                requiredItemIds: [BlackCleaverItemId])
        ]);

    private static CoachRuleDefinition CreateRule(
        string id,
        int minimumLevel,
        PowerState state,
        int priority,
        int? maximumLevel = null,
        IReadOnlyList<int>? requiredItemIds = null,
        IReadOnlyList<int>? excludedItemIds = null) =>
        new()
        {
            Id = id,
            PlayerChampion = "Urgot",
            OpponentChampion = "Sett",
            MinimumLevel = minimumLevel,
            MaximumLevel = maximumLevel,
            RequiredItemIds = requiredItemIds ?? [],
            ExcludedItemIds = excludedItemIds ?? [],
            PowerState = state,
            Priority = priority,
            Title = "Test title",
            Message = "Test message",
            Source = "Test source"
        };

    private static MatchState CreateMatch(
        int level,
        string opponentChampion = "Sett",
        params int[] itemIds)
    {
        var player = CreatePlayer(
            "active-player",
            "Urgot",
            TeamSide.Order,
            PlayerPosition.Top,
            level,
            itemIds);
        var opponent = CreatePlayer(
            "enemy-top",
            opponentChampion,
            TeamSide.Chaos,
            PlayerPosition.Top,
            level);

        return new MatchState
        {
            GameTimeSeconds = 90,
            ActivePlayerId = player.ParticipantId,
            Players = [player, opponent]
        };
    }

    private static MatchState CreateFullMatch(int level, params int[] itemIds)
    {
        PlayerState[] players =
        [
            CreatePlayer("active-player", "Urgot", TeamSide.Order, PlayerPosition.Top, level, itemIds),
            CreatePlayer("ally-jungle", "Amumu", TeamSide.Order, PlayerPosition.Jungle, level),
            CreatePlayer("ally-middle", "Ahri", TeamSide.Order, PlayerPosition.Middle, level),
            CreatePlayer("ally-bottom", "Jinx", TeamSide.Order, PlayerPosition.Bottom, level),
            CreatePlayer("ally-support", "Thresh", TeamSide.Order, PlayerPosition.Support, level),
            CreatePlayer("enemy-top", "Sett", TeamSide.Chaos, PlayerPosition.Top, level),
            CreatePlayer("enemy-jungle", "LeeSin", TeamSide.Chaos, PlayerPosition.Jungle, level),
            CreatePlayer("enemy-middle", "Syndra", TeamSide.Chaos, PlayerPosition.Middle, level),
            CreatePlayer("enemy-bottom", "Caitlyn", TeamSide.Chaos, PlayerPosition.Bottom, level),
            CreatePlayer("enemy-support", "Leona", TeamSide.Chaos, PlayerPosition.Support, level)
        ];

        return new MatchState
        {
            GameTimeSeconds = 90,
            ActivePlayerId = "active-player",
            Players = players
        };
    }

    private static PlayerState CreatePlayer(
        string id,
        string champion,
        TeamSide team,
        PlayerPosition position,
        int level,
        params int[] itemIds) =>
        new()
        {
            ParticipantId = id,
            ChampionName = champion,
            Team = team,
            Position = position,
            Level = level,
            ItemIds = itemIds.ToHashSet()
        };
}
