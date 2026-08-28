using TempoCue.Application;

namespace TempoCue.Application.Tests;

public sealed class ParticipantVisibilityTransitionDetectorTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Update_PublishesAppearanceOnlyAfterVisibleStateIsConfirmed()
    {
        var detector = new ParticipantVisibilityTransitionDetector();

        Assert.Empty(detector.Update(Start, [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(250),
            [Snapshot(ParticipantVisibilityStatus.Confirming)]));
        var transition = Assert.Single(detector.Update(
            Start.AddMilliseconds(500),
            [Snapshot(ParticipantVisibilityStatus.Visible)]));

        Assert.Equal(ParticipantVisibilityTransitionKind.Appeared, transition.Kind);
        Assert.Equal("enemy-jungle", transition.ParticipantId);
        Assert.Equal(Start.AddMilliseconds(500), transition.OccurredAt);
    }

    [Fact]
    public void Update_RequiresNegativeConfirmationBeforeDisappearance()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        BecomeVisible(detector, lastSeenAt: Start.AddMilliseconds(250));

        // Первые пустые кадры после истечения удержания — ещё не доказательство:
        // нужно и число успешных кадров без участника, и длительность отсутствия.
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(1500),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(1750),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(2000),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        var transition = Assert.Single(detector.Update(
            Start.AddMilliseconds(2250),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));

        Assert.Equal(ParticipantVisibilityTransitionKind.Disappeared, transition.Kind);
        Assert.Equal(Start.AddMilliseconds(250), transition.LastSeenAt);
    }

    [Fact]
    public void Update_PublishesLongAbsenceExactlyOnce()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        BecomeVisible(detector, lastSeenAt: Start.AddMilliseconds(250));

        var total = 0;
        for (var frame = 0; frame < 20; frame++)
        {
            total += detector.Update(
                Start.AddMilliseconds(1500 + (frame * 250)),
                [Snapshot(ParticipantVisibilityStatus.NotVisible)]).Count;
        }

        Assert.Equal(1, total);
    }

    [Fact]
    public void Update_BriefRecognitionDipDoesNotPublishAnything()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        BecomeVisible(detector, lastSeenAt: Start.AddMilliseconds(250));

        // Один-два кратких провала распознавания и возвращение видимости
        // не рождают ни исчезновения, ни повторного появления.
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(1500),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(1750),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(2000),
            [Snapshot(
                ParticipantVisibilityStatus.Visible,
                lastSeenAt: Start.AddMilliseconds(2000))]));
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(2250),
            [Snapshot(
                ParticipantVisibilityStatus.Visible,
                lastSeenAt: Start.AddMilliseconds(2250))]));
    }

    [Fact]
    public void Update_ConfirmingFrameCancelsPendingDisappearance()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        BecomeVisible(detector, lastSeenAt: Start.AddMilliseconds(250));
        _ = detector.Update(
            Start.AddMilliseconds(1500),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]);
        _ = detector.Update(
            Start.AddMilliseconds(1750),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]);

        // Наблюдение в кадре — доказательство присутствия: счёт пустых кадров
        // начинается заново, поэтому два следующих пропуска не подтверждают уход.
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(2000),
            [Snapshot(ParticipantVisibilityStatus.Confirming)]));
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(2250),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        Assert.Empty(detector.Update(
            Start.AddMilliseconds(2500),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
    }

    [Fact]
    public void Update_PostponesDisappearanceWhileVisibleNeighborCoversLastPosition()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        BecomeVisible(detector, lastSeenAt: Start.AddMilliseconds(250));

        // Сосед стоит в точке слияния значков: пропажа рамки неотличима от
        // окклюзии, подтверждение откладывается.
        for (var frame = 0; frame < 6; frame++)
        {
            Assert.Empty(detector.Update(
                Start.AddMilliseconds(1500 + (frame * 250)),
                [
                    Snapshot(ParticipantVisibilityStatus.NotVisible),
                    Neighbor(mapX: 0.41, mapY: 0.61)
                ]));
        }

        // Сосед отошёл — отсутствие подтверждается немедленно: и кадры, и
        // длительность уже накоплены.
        var transition = Assert.Single(detector.Update(
            Start.AddMilliseconds(3000),
            [
                Snapshot(ParticipantVisibilityStatus.NotVisible),
                Neighbor(mapX: 0.55, mapY: 0.75)
            ]));

        Assert.Equal(ParticipantVisibilityTransitionKind.Disappeared, transition.Kind);
        Assert.Equal("enemy-jungle", transition.ParticipantId);
    }

    [Fact]
    public void Update_DistantVisibleNeighborDoesNotPostponeDisappearance()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        BecomeVisible(detector, lastSeenAt: Start.AddMilliseconds(250));

        var transitions = new List<ParticipantVisibilityTransition>();
        for (var frame = 0; frame < 4; frame++)
        {
            transitions.AddRange(detector.Update(
                Start.AddMilliseconds(1500 + (frame * 250)),
                [
                    Snapshot(ParticipantVisibilityStatus.NotVisible),
                    Neighbor(mapX: 0.48, mapY: 0.68)
                ]));
        }

        Assert.Equal(
            ParticipantVisibilityTransitionKind.Disappeared,
            Assert.Single(transitions).Kind);
    }

    [Fact]
    public void Update_DoesNotTurnStreamRecoveryIntoVisibilityEvent()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        BecomeVisible(detector, lastSeenAt: Start.AddMilliseconds(250));

        Assert.Empty(detector.Update(
            Start.AddSeconds(3),
            [Snapshot(ParticipantVisibilityStatus.Unknown)]));
        // Первое устойчивое состояние после восстановления фиксируется молча,
        // каким бы оно ни было.
        Assert.Empty(detector.Update(
            Start.AddSeconds(4),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        Assert.Empty(detector.Update(
            Start.AddSeconds(5),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
        Assert.Empty(detector.Update(
            Start.AddSeconds(6),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
    }

    [Fact]
    public void Update_RecoveredStreamStillDetectsNextRealTransition()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        _ = detector.Update(Start, [Snapshot(ParticipantVisibilityStatus.Unknown)]);
        _ = detector.Update(
            Start.AddMilliseconds(250),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]);
        _ = detector.Update(
            Start.AddMilliseconds(500),
            [Snapshot(ParticipantVisibilityStatus.Confirming)]);

        var transition = Assert.Single(detector.Update(
            Start.AddMilliseconds(750),
            [Snapshot(ParticipantVisibilityStatus.Visible)]));

        Assert.Equal(ParticipantVisibilityTransitionKind.Appeared, transition.Kind);
    }

    [Fact]
    public void Reset_DropsPreviousMatchState()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        BecomeVisible(detector, lastSeenAt: Start.AddMilliseconds(250));

        detector.Reset();

        Assert.Empty(detector.Update(
            Start.AddSeconds(3),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
    }

    private static void BecomeVisible(
        ParticipantVisibilityTransitionDetector detector,
        DateTimeOffset lastSeenAt)
    {
        _ = detector.Update(Start, [Snapshot(ParticipantVisibilityStatus.NotVisible)]);
        _ = detector.Update(
            Start.AddMilliseconds(250),
            [Snapshot(ParticipantVisibilityStatus.Visible, lastSeenAt)]);
    }

    private static ParticipantVisibilitySnapshot Snapshot(
        ParticipantVisibilityStatus status,
        DateTimeOffset? lastSeenAt = null) =>
        new(
            "enemy-jungle",
            status,
            lastSeenAt ?? Start.AddMilliseconds(250),
            0.91,
            0.4,
            0.6);

    private static ParticipantVisibilitySnapshot Neighbor(double mapX, double mapY) =>
        new(
            "enemy-support",
            ParticipantVisibilityStatus.Visible,
            Start.AddMilliseconds(250),
            0.9,
            mapX,
            mapY);
}
