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
    public void Update_PublishesDisappearanceOnlyAfterVisibleStateExpires()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        _ = detector.Update(Start, [Snapshot(ParticipantVisibilityStatus.Confirming)]);
        _ = detector.Update(
            Start.AddMilliseconds(250),
            [Snapshot(ParticipantVisibilityStatus.Visible)]);

        Assert.Empty(detector.Update(
            Start.AddMilliseconds(750),
            [Snapshot(ParticipantVisibilityStatus.Visible)]));
        var transition = Assert.Single(detector.Update(
            Start.AddMilliseconds(1500),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));

        Assert.Equal(ParticipantVisibilityTransitionKind.Disappeared, transition.Kind);
        Assert.Equal(Start.AddMilliseconds(250), transition.LastSeenAt);
    }

    [Fact]
    public void Update_DoesNotTurnStreamRecoveryIntoVisibilityEvent()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        _ = detector.Update(Start, [Snapshot(ParticipantVisibilityStatus.Visible)]);

        Assert.Empty(detector.Update(
            Start.AddSeconds(1),
            [Snapshot(ParticipantVisibilityStatus.Unknown)]));
        Assert.Empty(detector.Update(
            Start.AddSeconds(2),
            [Snapshot(ParticipantVisibilityStatus.Visible)]));
    }

    [Fact]
    public void Reset_DropsPreviousMatchState()
    {
        var detector = new ParticipantVisibilityTransitionDetector();
        _ = detector.Update(Start, [Snapshot(ParticipantVisibilityStatus.Visible)]);

        detector.Reset();

        Assert.Empty(detector.Update(
            Start.AddSeconds(1),
            [Snapshot(ParticipantVisibilityStatus.NotVisible)]));
    }

    private static ParticipantVisibilitySnapshot Snapshot(
        ParticipantVisibilityStatus status) =>
        new(
            "enemy-jungle",
            status,
            Start.AddMilliseconds(250),
            0.91,
            0.4,
            0.6);
}
