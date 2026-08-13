namespace TempoCue.Core.Tests;

public sealed class MatchupResolverTests
{
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
    public void Resolve_SelectsEnemyOnSamePosition()
    {
        var matchup = MatchupResolver.Resolve(CreateFullMatch(level: 1));

        Assert.NotNull(matchup);
        Assert.Equal("Urgot", matchup.Player.ChampionName);
        Assert.Equal("Sett", matchup.Opponent.ChampionName);
        Assert.Equal(PlayerPosition.Top, matchup.Opponent.Position);
    }

    [Fact]
    public void Resolve_UsesOnlyEnemyInOneVersusOneWithoutPosition()
    {
        var match = UpdatePlayers(
            CreateMatch(level: 1),
            player => player with { Position = PlayerPosition.Unknown });

        var matchup = MatchupResolver.Resolve(match);

        Assert.NotNull(matchup);
        Assert.Equal("Sett", matchup.Opponent.ChampionName);
    }

    [Fact]
    public void Resolve_DoesNotGuessAmongSeveralEnemiesWithoutMatchingPosition()
    {
        var match = UpdatePlayers(
            CreateFullMatch(level: 1),
            player => player.Team == TeamSide.Chaos
                ? player with { Position = PlayerPosition.Unknown }
                : player);

        var matchup = MatchupResolver.Resolve(match);

        Assert.Null(matchup);
    }

    [Fact]
    public void Resolve_InfersTopForOnlyUnknownTeammate()
    {
        var match = UpdatePlayers(
            CreateFullMatch(level: 1),
            player => player.ParticipantId switch
            {
                "active-player" => player with
                {
                    ChampionName = "Anivia",
                    Position = PlayerPosition.Unknown
                },
                "ally-support" => player with { Position = PlayerPosition.Bottom },
                "enemy-top" => player with { ChampionName = "Teemo" },
                _ => player
            });

        var matchup = MatchupResolver.Resolve(match);

        Assert.NotNull(matchup);
        Assert.Equal(PlayerPosition.Top, matchup.Player.Position);
        Assert.Equal("Teemo", matchup.Opponent.ChampionName);
    }

    [Theory]
    [InlineData(PlayerPosition.Top)]
    [InlineData(PlayerPosition.Jungle)]
    [InlineData(PlayerPosition.Middle)]
    [InlineData(PlayerPosition.Bottom)]
    [InlineData(PlayerPosition.Support)]
    public void Resolve_InfersOnlyMissingPositionAndSelectsMatchingEnemy(
        PlayerPosition expectedPosition)
    {
        var matchup = MatchupResolver.Resolve(
            CreateMatchWithOnlyActivePositionUnknown(expectedPosition));

        Assert.NotNull(matchup);
        Assert.Equal(expectedPosition, matchup.Player.Position);
        Assert.Equal($"Enemy{expectedPosition}", matchup.Opponent.ChampionName);
        Assert.Equal(expectedPosition, matchup.Opponent.Position);
    }

    private static MatchState CreateMatch(int level)
    {
        var player = CreatePlayer(
            "active-player",
            "Urgot",
            TeamSide.Order,
            PlayerPosition.Top,
            level);
        var opponent = CreatePlayer(
            "enemy-top",
            "Sett",
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

    private static MatchState CreateFullMatch(int level)
    {
        PlayerState[] players =
        [
            CreatePlayer("active-player", "Urgot", TeamSide.Order, PlayerPosition.Top, level),
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

    private static MatchState CreateMatchWithOnlyActivePositionUnknown(
        PlayerPosition missingPosition)
    {
        PlayerPosition[] positions =
        [
            PlayerPosition.Top,
            PlayerPosition.Jungle,
            PlayerPosition.Middle,
            PlayerPosition.Bottom,
            PlayerPosition.Support
        ];

        var allies = positions
            .Where(position => position != missingPosition)
            .Select(position => CreatePlayer(
                $"ally-{position}",
                $"Ally{position}",
                TeamSide.Order,
                position,
                level: 1));
        var enemies = positions.Select(position => CreatePlayer(
            $"enemy-{position}",
            $"Enemy{position}",
            TeamSide.Chaos,
            position,
            level: 1));

        return new MatchState
        {
            GameTimeSeconds = 90,
            ActivePlayerId = "active-player",
            Players =
            [
                CreatePlayer(
                    "active-player",
                    "ActiveChampion",
                    TeamSide.Order,
                    PlayerPosition.Unknown,
                    level: 1),
                .. allies,
                .. enemies
            ]
        };
    }

    private static MatchState UpdatePlayers(
        MatchState match,
        Func<PlayerState, PlayerState> update) =>
        match with { Players = match.Players.Select(update).ToArray() };

    private static PlayerState CreatePlayer(
        string id,
        string champion,
        TeamSide team,
        PlayerPosition position,
        int level) =>
        new()
        {
            ParticipantId = id,
            ChampionName = champion,
            Team = team,
            Position = position,
            Level = level
        };
}
