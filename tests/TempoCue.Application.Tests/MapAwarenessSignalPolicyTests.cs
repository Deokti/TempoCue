using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class MapAwarenessSignalPolicyTests
{
    [Theory]
    [InlineData(
        PlayerPosition.Jungle,
        ParticipantVisibilityTransitionKind.Appeared,
        MapAwarenessSignalKind.EnemyJunglerAppeared)]
    [InlineData(
        PlayerPosition.Middle,
        ParticipantVisibilityTransitionKind.Disappeared,
        MapAwarenessSignalKind.EnemyMidDisappeared)]
    public void Evaluate_ReturnsExpectedEnemySignal(
        PlayerPosition position,
        ParticipantVisibilityTransitionKind transitionKind,
        MapAwarenessSignalKind expectedKind)
    {
        var occurredAt = DateTimeOffset.Parse("2026-08-21T12:00:00Z");
        var transition = CreateTransition("enemy", transitionKind, occurredAt);
        var match = CreateMatch(CreatePlayer("enemy", TeamSide.Chaos, position));

        var signal = MapAwarenessSignalPolicy.Evaluate(transition, match);

        Assert.NotNull(signal);
        Assert.Equal(expectedKind, signal.Kind);
        Assert.Equal("Enemy champion", signal.ChampionName);
        Assert.Equal(occurredAt, signal.OccurredAt);
        Assert.Equal(0.92, signal.Confidence);
    }

    [Theory]
    [InlineData(TeamSide.Order, PlayerPosition.Jungle, false)]
    [InlineData(TeamSide.Unknown, PlayerPosition.Jungle, false)]
    [InlineData(TeamSide.Chaos, PlayerPosition.Unknown, false)]
    [InlineData(TeamSide.Chaos, PlayerPosition.Jungle, true)]
    public void Evaluate_IgnoresUnsafeOrIrrelevantParticipants(
        TeamSide team,
        PlayerPosition position,
        bool isDead)
    {
        var transition = CreateTransition(
            "target",
            ParticipantVisibilityTransitionKind.Appeared,
            DateTimeOffset.UtcNow);
        var match = CreateMatch(CreatePlayer("target", team, position, isDead));

        Assert.Null(MapAwarenessSignalPolicy.Evaluate(transition, match));
    }

    [Fact]
    public void Evaluate_IgnoresTransitionThatDoesNotMatchRolePolicy()
    {
        var transition = CreateTransition(
            "enemy",
            ParticipantVisibilityTransitionKind.Disappeared,
            DateTimeOffset.UtcNow);
        var match = CreateMatch(CreatePlayer("enemy", TeamSide.Chaos, PlayerPosition.Jungle));

        Assert.Null(MapAwarenessSignalPolicy.Evaluate(transition, match));
    }

    [Theory]
    [InlineData(0.66)]
    [InlineData(MapAwarenessSignalPolicy.MinimumOperationalConfidence - 0.001)]
    public void Evaluate_IgnoresLowConfidenceAppearance(double confidence)
    {
        var transition = CreateTransition(
            "enemy",
            ParticipantVisibilityTransitionKind.Appeared,
            DateTimeOffset.UtcNow,
            confidence);
        var match = CreateMatch(CreatePlayer(
            "enemy",
            TeamSide.Chaos,
            PlayerPosition.Jungle));

        Assert.Null(MapAwarenessSignalPolicy.Evaluate(transition, match));
    }

    [Fact]
    public void Evaluate_AcceptsAppearanceAtOperationalConfidenceBoundary()
    {
        var transition = CreateTransition(
            "enemy",
            ParticipantVisibilityTransitionKind.Appeared,
            DateTimeOffset.UtcNow,
            MapAwarenessSignalPolicy.MinimumOperationalConfidence);
        var match = CreateMatch(CreatePlayer(
            "enemy",
            TeamSide.Chaos,
            PlayerPosition.Jungle));

        Assert.NotNull(MapAwarenessSignalPolicy.Evaluate(transition, match));
    }

    private static ParticipantVisibilityTransition CreateTransition(
        string participantId,
        ParticipantVisibilityTransitionKind kind,
        DateTimeOffset occurredAt,
        double confidence = 0.92) => new(
            participantId,
            kind,
            occurredAt,
            occurredAt.AddSeconds(-1),
            confidence,
            0.4,
            0.6);

    private static MatchState CreateMatch(PlayerState target) => new()
    {
        GameTimeSeconds = 120,
        ActivePlayerId = "active",
        Players =
        [
            CreatePlayer("active", TeamSide.Order, PlayerPosition.Top),
            target
        ]
    };

    private static PlayerState CreatePlayer(
        string participantId,
        TeamSide team,
        PlayerPosition position,
        bool isDead = false) => new()
    {
        ParticipantId = participantId,
        ChampionName = participantId == "active" ? "Urgot" : "Enemy champion",
        Team = team,
        Position = position,
        Level = 6,
        IsDead = isDead
    };
}
