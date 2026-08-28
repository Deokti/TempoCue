using TempoCue.Application;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class MapAwarenessSignalPolicyTests
{
    [Theory]
    [InlineData(PlayerPosition.Jungle, ParticipantVisibilityTransitionKind.Appeared)]
    [InlineData(PlayerPosition.Middle, ParticipantVisibilityTransitionKind.Disappeared)]
    [InlineData(PlayerPosition.Support, ParticipantVisibilityTransitionKind.Disappeared)]
    public void Evaluate_ReturnsTheMatchingRule(
        PlayerPosition position,
        ParticipantVisibilityTransitionKind transitionKind)
    {
        // Третий случай важнее первых двух: саппорта в коде нет и не было, он
        // работает только потому, что сигналы больше не зашиты. ADR 0016.
        var occurredAt = DateTimeOffset.Parse("2026-08-21T12:00:00Z");
        var transition = CreateTransition("enemy", transitionKind, occurredAt);
        var match = CreateMatch(CreatePlayer("enemy", TeamSide.Chaos, position));
        var rule = CreateRule(position, ToVisionTransition(transitionKind));

        var signal = MapAwarenessSignalPolicy.Evaluate(transition, match, [rule]);

        Assert.NotNull(signal);
        Assert.Same(rule, signal.Rule);
        Assert.Equal("Enemy champion", signal.ChampionName);
        Assert.Equal(occurredAt, signal.OccurredAt);
        Assert.Equal(0.92, signal.Confidence);
    }

    [Fact]
    public void Evaluate_ReturnsNothingWhenNoPackDescribesTheEvent()
    {
        var transition = CreateTransition(
            "enemy",
            ParticipantVisibilityTransitionKind.Appeared,
            DateTimeOffset.UtcNow);
        var match = CreateMatch(CreatePlayer("enemy", TeamSide.Chaos, PlayerPosition.Jungle));

        // Игрок выключил все наборы — уведомлять не о чем.
        Assert.Null(MapAwarenessSignalPolicy.Evaluate(transition, match, []));
    }

    [Fact]
    public void Evaluate_TakesTheFirstMatchingRuleInAuthorOrder()
    {
        var transition = CreateTransition(
            "enemy",
            ParticipantVisibilityTransitionKind.Appeared,
            DateTimeOffset.UtcNow);
        var match = CreateMatch(CreatePlayer("enemy", TeamSide.Chaos, PlayerPosition.Jungle));
        var first = CreateRule(PlayerPosition.Jungle, MapVisionTransition.Appeared, "first");
        var second = CreateRule(PlayerPosition.Jungle, MapVisionTransition.Appeared, "second");

        var signal = MapAwarenessSignalPolicy.Evaluate(transition, match, [first, second]);

        // Два набора могут описать одно событие; два уведомления об одном
        // наблюдении игроку не нужны.
        Assert.Same(first, signal?.Rule);
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

        Assert.Null(MapAwarenessSignalPolicy.Evaluate(transition, match, AllRules));
    }

    [Fact]
    public void Evaluate_IgnoresEventNoRuleDescribes()
    {
        var transition = CreateTransition(
            "enemy",
            ParticipantVisibilityTransitionKind.Disappeared,
            DateTimeOffset.UtcNow);
        var match = CreateMatch(CreatePlayer("enemy", TeamSide.Chaos, PlayerPosition.Jungle));
        var onlyAppearance = CreateRule(PlayerPosition.Jungle, MapVisionTransition.Appeared);

        // Набор описывает появление лесника, но не его пропажу. Уведомлять не о чем:
        // раньше это решал зашитый switch, теперь — содержание набора.
        Assert.Null(MapAwarenessSignalPolicy.Evaluate(transition, match, [onlyAppearance]));
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

        Assert.Null(MapAwarenessSignalPolicy.Evaluate(transition, match, AllRules));
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

        Assert.NotNull(MapAwarenessSignalPolicy.Evaluate(transition, match, AllRules));
    }

    /// <summary>Правила на все роли и оба события: проверяем политику, а не набор.</summary>
    private static readonly IReadOnlyList<MapVisionRule> AllRules =
    [
        .. Enum.GetValues<PlayerPosition>()
            .Where(static position => position is not PlayerPosition.Unknown)
            .SelectMany(static position => Enum.GetValues<MapVisionTransition>()
                .Select(transition => CreateRule(position, transition)))
    ];

    private static MapVisionRule CreateRule(
        PlayerPosition position,
        MapVisionTransition transition,
        string? id = null) => new(
            id ?? $"{position}-{transition}",
            position,
            transition,
            MapObjectiveReminderPriority.Caution,
            "Сообщение",
            "Почему сейчас",
            "Что дальше");

    private static MapVisionTransition ToVisionTransition(
        ParticipantVisibilityTransitionKind kind) =>
        kind is ParticipantVisibilityTransitionKind.Appeared
            ? MapVisionTransition.Appeared
            : MapVisionTransition.Disappeared;

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
