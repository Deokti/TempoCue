using System.Text;

namespace RiftCoach.Core.Tests;

/// <summary>
/// Автоматические проверки движка матчапов. Каждый тест создаёт известное состояние
/// и сравнивает полученный результат с ожидаемым.
/// </summary>
public sealed class MatchupCoachTests
{
    private const int BlackCleaverItemId = 3071;

    [Fact]
    public void GetBestAdvice_ReturnsDangerousAdvice_ForEarlyLane()
    {
        // Arrange: подготавливаем движок и его правила.
        var coach = CreateCoach();

        // Act: запрашиваем совет для первого уровня.
        var advice = coach.GetBestAdvice(CreateMatch(level: 1));

        // Assert: проверяем, что движок выбрал ожидаемое правило.
        Assert.NotNull(advice);
        Assert.Equal(PowerState.Dangerous, advice.PowerState);
        Assert.Equal("early", advice.RuleId);
    }

    [Fact]
    public void GetBestAdvice_ReturnsCautiousAdvice_AtLevelNineWithoutItem()
    {
        var coach = CreateCoach();

        var advice = coach.GetBestAdvice(CreateMatch(level: 9));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Cautious, advice.PowerState);
        Assert.Equal("waiting-item", advice.RuleId);
    }

    [Fact]
    public void GetBestAdvice_ReturnsPlayableAdvice_WhenPowerSpikeIsComplete()
    {
        var coach = CreateCoach();

        var advice = coach.GetBestAdvice(CreateMatch(level: 9, itemIds: [BlackCleaverItemId]));

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Playable, advice.PowerState);
        Assert.Equal("power-spike", advice.RuleId);
    }

    [Fact]
    public void GetBestAdvice_ReturnsNull_ForUnknownMatchup()
    {
        var coach = CreateCoach();
        var match = CreateMatch(level: 1, opponentChampion: "Garen");

        var advice = coach.GetBestAdvice(match);

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

    [Fact]
    public void CoachPackLoader_LoadsActualUrgotVersusSettPack()
    {
        // Этот тест открывает тот же JSON, который использует WPF-приложение.
        var packPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "CoachPacks",
            "urgot-vs-sett.json");

        var pack = CoachPackLoader.LoadFromFile(packPath);

        Assert.Equal("top.urgot-vs-sett.prototype", pack.Id);
        Assert.Equal(3, pack.Rules.Count);
        Assert.All(pack.Rules, rule => Assert.Contains("GoliathGames", rule.Source));
    }

    [Fact]
    public void MatchState_ReturnsAlliesAndEnemies_ForFullTeamComposition()
    {
        var match = CreateFullMatch(level: 1);

        var activePlayer = match.GetActivePlayer();
        var allies = match.GetAllies();
        var enemies = match.GetEnemies();

        Assert.NotNull(activePlayer);
        Assert.Equal("Urgot", activePlayer.ChampionName);
        Assert.Equal(4, allies.Count);
        Assert.Equal(5, enemies.Count);
        Assert.All(allies, ally => Assert.Equal(TeamSide.Order, ally.Team));
        Assert.All(enemies, enemy => Assert.Equal(TeamSide.Chaos, enemy.Team));
    }

    [Fact]
    public void MatchupResolver_SelectsEnemyOnSamePosition()
    {
        var match = CreateFullMatch(level: 1);

        var matchup = MatchupResolver.Resolve(match);

        Assert.NotNull(matchup);
        Assert.Equal("Urgot", matchup.Player.ChampionName);
        Assert.Equal("Sett", matchup.Opponent.ChampionName);
        Assert.Equal(PlayerPosition.Top, matchup.Opponent.Position);
    }

    [Fact]
    public void GetBestAdvice_UsesAllParticipantsAndSelectsTopMatchup()
    {
        var coach = CreateCoach();
        var match = CreateFullMatch(level: 9, itemIds: [BlackCleaverItemId]);

        var advice = coach.GetBestAdvice(match);

        Assert.NotNull(advice);
        Assert.Equal(PowerState.Playable, advice.PowerState);
        Assert.Equal("power-spike", advice.RuleId);
    }

    [Fact]
    public void MatchupResolver_UsesOnlyEnemyInOneVersusOneWithoutPosition()
    {
        var match = CreateMatch(level: 1);
        match = match with
        {
            Players = match.Players
                .Select(player => player with { Position = PlayerPosition.Unknown })
                .ToArray()
        };

        var matchup = MatchupResolver.Resolve(match);

        Assert.NotNull(matchup);
        Assert.Equal("Sett", matchup.Opponent.ChampionName);
    }

    [Fact]
    public void MatchupResolver_DoesNotGuessAmongSeveralEnemiesWithoutMatchingPosition()
    {
        var match = CreateFullMatch(level: 1);
        match = match with
        {
            Players = match.Players
                .Select(player => player.Team == TeamSide.Chaos
                    ? player with { Position = PlayerPosition.Unknown }
                    : player)
                .ToArray()
        };

        var matchup = MatchupResolver.Resolve(match);

        // Лучше честно не выдать совет, чем назначить случайного врага оппонентом на линии.
        Assert.Null(matchup);
    }

    private static MatchupCoach CreateCoach()
    {
        // В тестах правила создаются прямо в C#, чтобы проверять только движок,
        // независимо от файловой системы и интерфейса приложения.
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

    private static MatchState CreateMatch(
        int level,
        string opponentChampion = "Sett",
        params int[] itemIds)
    {
        var player = CreatePlayer(
            id: "active-player",
            champion: "Urgot",
            team: TeamSide.Order,
            position: PlayerPosition.Top,
            level: level,
            itemIds: itemIds);

        var opponent = CreatePlayer(
            id: "enemy-top",
            champion: opponentChampion,
            team: TeamSide.Chaos,
            position: PlayerPosition.Top,
            level: level);

        return new MatchState
        {
            GameTimeSeconds = 90,
            ActivePlayerId = player.ParticipantId,
            Players = [player, opponent]
        };
    }

    private static MatchState CreateFullMatch(int level, params int[] itemIds)
    {
        // Состав специально содержит десять участников и по одному игроку
        // каждой позиции в обеих командах, как в обычном матче 5×5.
        var players = new PlayerState[]
        {
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
        };

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
        params int[] itemIds)
    {
        return new PlayerState
        {
            ParticipantId = id,
            ChampionName = champion,
            Team = team,
            Position = position,
            Level = level,
            ItemIds = itemIds.ToHashSet()
        };
    }
}
