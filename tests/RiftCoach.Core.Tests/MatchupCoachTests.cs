using System.Text;

namespace RiftCoach.Core.Tests;

public sealed class MatchupCoachTests
{
    private const int BlackCleaverItemId = 3071;

    [Fact]
    public void GetBestAdvice_ReturnsDangerousAdvice_ForEarlyLane()
    {
        var coach = CreateCoach();

        var advice = coach.GetBestAdvice(CreateState(level: 1));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Dangerous, advice.PowerState);
        Assert.Equal("early", advice.RuleId);
    }

    [Fact]
    public void GetBestAdvice_ReturnsCautiousAdvice_AtLevelNineWithoutItem()
    {
        var coach = CreateCoach();

        var advice = coach.GetBestAdvice(CreateState(level: 9));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Cautious, advice.PowerState);
        Assert.Equal("waiting-item", advice.RuleId);
    }

    [Fact]
    public void GetBestAdvice_ReturnsPlayableAdvice_WhenPowerSpikeIsComplete()
    {
        var coach = CreateCoach();

        var advice = coach.GetBestAdvice(CreateState(level: 9, BlackCleaverItemId));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Playable, advice.PowerState);
        Assert.Equal("power-spike", advice.RuleId);
    }

    [Fact]
    public void GetBestAdvice_ReturnsNull_ForUnknownMatchup()
    {
        var coach = CreateCoach();
        var state = CreateState(level: 1) with { OpponentChampion = "Garen" };

        var advice = coach.GetBestAdvice(state);

        Assert.Null(advice);
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

    private static MatchupCoach CreateCoach()
    {
        var rules = new[]
        {
            CreateRule(
                id: "early",
                minimumLevel: 1,
                maximumLevel: 8,
                state: PowerState.Dangerous,
                priority: 80),
            CreateRule(
                id: "waiting-item",
                minimumLevel: 9,
                state: PowerState.Cautious,
                priority: 85,
                excludedItemIds: [BlackCleaverItemId]),
            CreateRule(
                id: "power-spike",
                minimumLevel: 9,
                state: PowerState.Playable,
                priority: 100,
                requiredItemIds: [BlackCleaverItemId])
        };

        return new MatchupCoach(rules);
    }

    private static CoachRuleDefinition CreateRule(
        string id,
        int minimumLevel,
        PowerState state,
        int priority,
        int? maximumLevel = null,
        IReadOnlyList<int>? requiredItemIds = null,
        IReadOnlyList<int>? excludedItemIds = null)
    {
        return new CoachRuleDefinition
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
    }

    private static GameState CreateState(int level, params int[] itemIds)
    {
        return new GameState
        {
            PlayerChampion = "Urgot",
            OpponentChampion = "Sett",
            PlayerLevel = level,
            PlayerItemIds = itemIds.ToHashSet()
        };
    }
}
